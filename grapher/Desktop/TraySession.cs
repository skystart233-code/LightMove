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
        private bool exiting, hinted;
        private DesktopSettingsForm settingsForm;
        public bool IsHidden => !home.Visible;
        public TraySession(CurveEditorForm home,DesktopSettingsService settings,bool background,EventWaitHandle showSignal=null,EventWaitHandle exitSignal=null)
        {
            this.home=home; this.settings=settings; this.showSignal=showSignal;
            UpdateCloseLabel();
            var menu=new ContextMenuStrip();
            menu.Items.Add("打开轻移",null,(s,e)=>Restore());
            menu.Items.Add("启动与托盘设置",null,(s,e)=>OpenSettings());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出轻移",null,(s,e)=>RequestExit());
            tray=new NotifyIcon {Text="轻移 LightMove · 稳、准、快",Icon=home.Icon??SystemIcons.Application,ContextMenuStrip=menu,Visible=true};
            tray.DoubleClick+=(s,e)=>Restore();
            home.FormClosing+=Closing;
            home.FormClosed+=(s,e)=> { tray.Visible=false; ExitThread(); };
            home.Resize+=(s,e)=> { if(home.WindowState==FormWindowState.Minimized && settings.Current.CloseToTray) HideToTray(); };
            home.DesktopSettingsRequested+=(s,e)=>OpenSettings();
            signalTimer=new System.Windows.Forms.Timer {Interval=250};
            signalTimer.Tick+=(s,e)=>
            {
                if(exitSignal!=null && exitSignal.WaitOne(0)) { RequestExit(); return; }
                if(showSignal!=null && showSignal.WaitOne(0)) Restore();
            };
            signalTimer.Start();
            if(background && settings.Current.StartInTray)
            {
                // Create the home HWND without ever flashing an editable window.
                var handle=home.Handle;
            }
            else home.Show();
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
                signalTimer.Stop(); signalTimer.Dispose(); tray.Visible=false;
                tray.ContextMenuStrip?.Dispose(); tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
