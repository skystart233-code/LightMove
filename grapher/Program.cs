using System;
using System.Globalization;
using System.Windows.Forms;

namespace grapher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Windows Run entries do not provide a working directory. Upstream
            // settings readers use relative paths, so anchor them before loading.
            System.IO.Directory.SetCurrentDirectory(Application.StartupPath);
            if(args.Length>0 && args[0]=="--cursor-recovery")
            {Desktop.CursorRecoveryGuard.Run(args);return;}
            if (Array.IndexOf(args, "--install-driver-helper") >= 0)
            {
                CurveEditing.FirstRunForm.InstallDriverHelper();
                return;
            }
            var mutex = new System.Threading.Mutex(true, "RawAccelGrapher", out bool result);

            if (!result)
            {
                bool updateExit=Array.IndexOf(args,"--exit-for-update")>=0;
                if(updateExit || Array.IndexOf(args,"--background")<0)
                    try { using(var signal=System.Threading.EventWaitHandle.OpenExisting(updateExit ? "Local\\LightMoveExit" : "Local\\LightMoveShow")) signal.Set(); }
                    catch(System.Threading.WaitHandleCannotBeOpenedException) { MessageBox.Show("轻移已在运行，请从系统托盘打开现有窗口。", "轻移 LightMove"); }
                return;
            }
            if(Array.IndexOf(args,"--exit-for-update")>=0) return;

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool reopen;
            do
            {
                reopen = false;
                try
                {
                    using (var backend = new RawAcceleration())
                    {
                        if (Array.IndexOf(args, "--legacy") >= 0) Application.Run(backend);
                        else using (var home = backend.CreateCurveEditor())
                        {
                            home.ShowInTaskbar = true;
                            if (Array.IndexOf(args, "--check-live") >= 0)
                            {
                                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath, "startup-check.json"),
                                    Newtonsoft.Json.JsonConvert.SerializeObject(new { Page = "LightMove", Driver = "connected", TrackedHandles = backend.AccelGUI.Settings.ActiveNormTaggedHandles.Count,
                                        WorkingDirectory=Environment.CurrentDirectory, Background=Array.IndexOf(args,"--background")>=0 }));
                                var check = new Timer { Interval = 1000 };
                                check.Tick += (s, e) =>
                                {
                                    var sample = backend.AccelGUI.AccelCharts.GetLiveSample();
                                    if (sample != null)
                                        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath, "live-input-check.json"), Newtonsoft.Json.JsonConvert.SerializeObject(sample));
                                    var watcher = backend.AccelGUI.MouseWatcher;
                                    System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath, "live-input-health.json"),
                                        Newtonsoft.Json.JsonConvert.SerializeObject(new { CheckedUtc = DateTime.UtcNow, watcher.InputMessageCount,
                                            watcher.MotionSampleCount, watcher.LastInputUtc, Receiver = watcher.InputWindowHandle.ToInt64(),
                                            TrackedHandles = backend.AccelGUI.Settings.ActiveNormTaggedHandles.Count, Sample = sample,
                                            home.NativeCaptionPresent, BorderStyle=home.FormBorderStyle.ToString(), WindowState=home.WindowState.ToString() }));
                                };
                                home.Shown += (s, e) => check.Start();
                                home.FormClosed += (s, e) => check.Dispose();
                            }
                            var preferences=Desktop.DesktopSettingsService.CreateDefault();
                            try { preferences.RefreshStartupTarget(); }
                            catch(Exception error) { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath,"startup-registration-error.log"),error.Message); }
                            using(var signal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\LightMoveShow"))
                            using(var exitSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\LightMoveExit"))
                            using(var session=new Desktop.TraySession(home,preferences,Array.IndexOf(args,"--background")>=0,signal,exitSignal))
                            {
                                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath,"desktop-session.json"),
                                    Newtonsoft.Json.JsonConvert.SerializeObject(new {Tray=true, Hidden=session.IsHidden,
                                        preferences.Current.StartInTray, preferences.Current.CloseToTray}));
                                Application.Run(session);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is InteropException || ex is System.IO.FileNotFoundException || ex is DllNotFoundException)
                {
                    using (var setup = new CurveEditing.FirstRunForm(ex.Message))
                    {
                        Application.Run(setup);
                        reopen = setup.ReadyToOpen;
                    }
                }
            } while (reopen);

            GC.KeepAlive(mutex);      
        }

        static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            System.IO.File.WriteAllText("error.log", ex.ToString());
            MessageBox.Show(Common.Chinese.ErrorMessage(ex.Message), "错误");
        }
    }
}
