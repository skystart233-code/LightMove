using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using grapher.CurveEditing;

namespace grapher.Desktop
{
    public sealed class TraySession : ApplicationContext
    {
        private readonly CurveEditorForm home;
        private readonly DesktopSettingsService settings;
        private readonly NotifyIcon tray;
        private readonly System.Windows.Forms.Timer signalTimer;
        private readonly EventWaitHandle showSignal;
        private readonly CursorLocator cursorLocator;
        private readonly ToolStripMenuItem shakeMenu;
        private readonly bool startCursorLocator;
        private bool exiting, hinted;
        private DesktopSettingsForm settingsForm;
        public bool IsHidden => !home.Visible;
        public bool LocatorEnabled => cursorLocator.Enabled;
        public int LocatorTriggerCount => cursorLocator.TriggerCount;
        public string LocatorLastError => cursorLocator.LastError;
        public TraySession(CurveEditorForm home,DesktopSettingsService settings,bool background,EventWaitHandle showSignal=null,EventWaitHandle exitSignal=null,bool startCursorLocator=true)
        {
            this.home=home; this.settings=settings; this.showSignal=showSignal; this.startCursorLocator=startCursorLocator;
            UpdateCloseLabel();
            var menu=new ContextMenuStrip();
            menu.Items.Add("打开轻移",null,(s,e)=>Restore());
            menu.Items.Add("偏好设置",null,(s,e)=>OpenSettings());
            cursorLocator=new CursorLocator(startCursorLocator && settings.Current.ShakeToFindCursor);
            shakeMenu=new ToolStripMenuItem("摇晃放大指针") {Checked=settings.Current.ShakeToFindCursor};
            shakeMenu.Click+=(s,e)=>
            {
                try
                {
                    settings.SetShakeToFindCursor(!shakeMenu.Checked);
                }
                catch(Exception error) { MessageBox.Show(home,"偏好保存未完成："+error.Message,"轻移 LightMove"); }
            };
            menu.Items.Add(shakeMenu);
            settings.Changed+=PreferencesChanged;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出轻移",null,(s,e)=>RequestExit());
            tray=new NotifyIcon {Text="轻移 LightMove · 稳、准、快",Icon=home.Icon??SystemIcons.Application,ContextMenuStrip=menu,Visible=true};
            cursorLocator.Failed+=(s,e)=>
            {
                shakeMenu.Checked=false;
                home.SetShakeCursorState(false,"暂时不可用，点击开关重试；曲线手感不受影响。");
                tray.BalloonTipTitle="摇晃放大暂不可用";
                tray.BalloonTipText="指针显示失败，已暂停此功能。可在偏好设置中关闭后重新开启。鼠标手感不受影响。";
                tray.ShowBalloonTip(3000);
            };
            tray.DoubleClick+=(s,e)=>Restore();
            home.FormClosing+=Closing;
            home.FormClosed+=(s,e)=> { tray.Visible=false; ExitThread(); };
            home.Resize+=(s,e)=> { if(home.WindowState==FormWindowState.Minimized && settings.Current.CloseToTray) HideToTray(); };
            home.DesktopSettingsRequested+=(s,e)=>OpenSettings();
            home.ShakeCursorToggleRequested+=ToggleShakeCursor;
            SyncShakeCursorState();
            signalTimer=new System.Windows.Forms.Timer {Interval=250};
            signalTimer.Tick+=(s,e)=>
            {
                if(exitSignal!=null && exitSignal.WaitOne(0)) { RequestExit(); return; }
                if(showSignal!=null && showSignal.WaitOne(0)) Restore();
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--check-live")>=0)
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath,"cursor-locator-health.json"),
                        Newtonsoft.Json.JsonConvert.SerializeObject(new { LocatorEnabled,LocatorTriggerCount,LocatorLastError }));
            };
            signalTimer.Start();
            if(background && settings.Current.StartInTray)
            {
                // Create the home HWND without ever flashing an editable window.
                var handle=home.Handle;
            }
            else home.Show();
        }
        private void PreferencesChanged(object sender,EventArgs e)
        {
            cursorLocator.SetEnabled(startCursorLocator && settings.Current.ShakeToFindCursor);
            SyncShakeCursorState();
        }
        private void SyncShakeCursorState()
        {
            bool enabled=startCursorLocator ? cursorLocator.Enabled : settings.Current.ShakeToFindCursor;
            shakeMenu.Checked=enabled;
            home.SetShakeCursorState(enabled,cursorLocator.LastError==null ? null : "暂时不可用，点击开关重试；曲线手感不受影响。");
        }
        private void ToggleShakeCursor(object sender,EventArgs e)
        {
            try {settings.SetShakeToFindCursor(!home.ShakeCursorEnabled);}
            catch {home.SetShakeCursorState(home.ShakeCursorEnabled,"保存未完成，设置保持原样。请再试一次。");}
        }
        private void Closing(object sender,FormClosingEventArgs e)
        {
            if(e.Cancel || exiting || e.CloseReason!=CloseReason.UserClosing || !settings.Current.CloseToTray) return;
            e.Cancel=true; HideToTray();
        }
        public void HideToTray()
        {
            home.Hide();
            if(!hinted)
            {
                hinted=true; tray.BalloonTipTitle="轻移已收进系统托盘";
                tray.BalloonTipText="双击图标恢复窗口，右键图标可退出。未应用的调整仍保留。";
                tray.ShowBalloonTip(1800);
            }
        }
        public void Restore()
        {
            home.ShowInTaskbar=true; home.Show(); home.WindowState=FormWindowState.Normal; home.Activate();
        }
        public void OpenSettings()
        {
            Restore();
            if(settingsForm!=null) { settingsForm.Activate(); return; }
            using(settingsForm=new DesktopSettingsForm(settings)) settingsForm.ShowDialog(home);
            settingsForm=null; UpdateCloseLabel();
        }
        private void UpdateCloseLabel()
        {
            var controls=home.Controls.Find("CloseWindow",true);
            if(controls.Length>0) controls[0].Text=settings.Current.CloseToTray ? "收起窗口" : "关闭窗口";
        }
        public void RequestExit()
        {
            if(!home.Enabled) { Restore(); return; }
            exiting=true; home.Close();
            if(!home.IsDisposed) exiting=false;
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                settings.Changed-=PreferencesChanged;home.ShakeCursorToggleRequested-=ToggleShakeCursor; cursorLocator.Dispose();
                signalTimer.Stop(); signalTimer.Dispose(); tray.Visible=false;
                tray.ContextMenuStrip?.Dispose(); tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
