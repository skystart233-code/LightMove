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
        static void Main()
        {
            var mutex = new System.Threading.Mutex(true, "RawAccelGrapher", out bool result);

            if (!result)
            {
                MessageBox.Show("Raw Accel 已在运行，请先关闭现有窗口。");
                return;
            }

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");

            AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledExceptionHandler;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RawAcceleration());

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
