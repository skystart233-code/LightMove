using System;
using System.Drawing;
using System.Windows.Forms;
using grapher.CurveEditing;

namespace grapher.Desktop
{
    public sealed class DesktopSettingsForm : StudioForm
    {
        private readonly StudioCheckBox autoStart, startInTray, closeToTray, shakeCursor;
        private readonly Label result;
        public DesktopSettingsForm(DesktopSettingsService settings)
        {
            SuspendLayout(); AutoScaleMode=AutoScaleMode.None; Resizable=false; MaximizeBox=false; MinimizeBox=false;
            Name="DesktopSettingsForm"; Text="偏好设置 · 轻移"; ClientSize=new Size(660,574);
            StartPosition=FormStartPosition.CenterParent; BackColor=LightTheme.Page; ForeColor=LightTheme.Ink;
            Font=new Font(LightTheme.FontFamily,10); ModernWindow.Attach(this);
            var viewport=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Name="PreferencesViewport"};
            var layout=new TableLayoutPanel {Dock=DockStyle.Top,Height=574,Padding=new Padding(28,14,28,20),ColumnCount=1,RowCount=11};
            foreach(int height in new[]{56,36,58,36,42,36,42,36,48,40,50}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            viewport.Controls.Add(layout); Controls.Add(viewport);
            layout.Controls.Add(new Label {Text="随时就绪，安静陪伴。",Font=new Font(Font.FontFamily,20,FontStyle.Bold),AutoSize=true});
            shakeCursor=Switch("摇晃鼠标，放大指针","ShakeToFindCursor",settings.Current.ShakeToFindCursor);
            layout.Controls.Add(shakeCursor);
            layout.Controls.Add(Explanation("快速来回摇晃即可找回指针，停下后自动恢复。\n按住鼠标键拖拽时暂停；收进托盘后仍可使用。"));
            autoStart=Switch("登录 Windows 后自动启动轻移","AutoStart",settings.AutoStart);
            startInTray=Switch("开机启动时直接收进系统托盘","StartInTray",settings.Current.StartInTray);
            closeToTray=Switch("关闭或最小化时收进系统托盘","CloseToTray",settings.Current.CloseToTray);
            layout.Controls.Add(autoStart);
            layout.Controls.Add(Explanation("仅为当前 Windows 用户设置，无需管理员权限。"));
            layout.Controls.Add(startInTray);
            layout.Controls.Add(Explanation("后台加载已保存的曲线；双击托盘图标即可打开界面。"));
            layout.Controls.Add(closeToTray);
            layout.Controls.Add(Explanation("窗口收起后保留未应用的调整。右键托盘图标，选择「退出轻移」可结束软件。"));
            result=new Label {Dock=DockStyle.Fill,ForeColor=LightTheme.Secondary,Text="偏好独立保存，不会改变鼠标曲线或系统指针方案。"}; layout.Controls.Add(result);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
            var save=new StudioButton {Name="SaveDesktopSettings",Text="保存设置",Size=new Size(120,36),BackColor=LightTheme.Accent,ForeColor=LightTheme.AccentInk};
            var cancel=new StudioButton {Text="取消",Size=new Size(96,36),BackColor=LightTheme.SurfaceAlt,ForeColor=LightTheme.Ink,DialogResult=DialogResult.Cancel};
            actions.Controls.Add(save); actions.Controls.Add(cancel); layout.Controls.Add(actions); CancelButton=cancel;
            startInTray.Enabled=autoStart.Checked; autoStart.CheckedChanged+=(s,e)=>startInTray.Enabled=autoStart.Checked;
            save.Click+=(s,e)=>
            {
                try
                {
                    settings.Save(autoStart.Checked,new DesktopPreferences {StartInTray=startInTray.Checked,CloseToTray=closeToTray.Checked,ShakeToFindCursor=shakeCursor.Checked});
                    DialogResult=DialogResult.OK; Close();
                }
                catch(Exception error) { result.ForeColor=LightTheme.Live; result.Text="保存未完成："+error.Message; }
            };
            using(var graphics=CreateGraphics()) Scale(new SizeF(graphics.DpiX/96f,graphics.DpiY/96f));
            ResumeLayout(true);
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var area=Screen.FromControl(this).WorkingArea;
            Size=new Size(Math.Min(Width,area.Width-24),Math.Min(Height,area.Height-24));
            CenterToScreen();
        }
        private StudioCheckBox Switch(string text,string name,bool value) => new StudioCheckBox {Name=name,Text=text,Checked=value,Dock=DockStyle.Fill,Margin=Padding.Empty};
        private Label Explanation(string text) => new Label {Text=text,Dock=DockStyle.Fill,ForeColor=LightTheme.Secondary,Margin=new Padding(42,3,0,0)};
    }
}
