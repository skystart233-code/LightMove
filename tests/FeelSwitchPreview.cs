// Offline eight-state visual wrapper; no driver writes or application settings.
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using grapher.CurveEditing;

static class FeelSwitchPreview
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp);
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        using(var form=new Form { FormBorderStyle=FormBorderStyle.None, ClientSize=new Size(800,520), BackColor=LightTheme.Page, AutoScaleMode=AutoScaleMode.None, ShowInTaskbar=false, Opacity=0, Font=new Font(LightTheme.FontFamily,9) })
        {
            FeelSwitch focused=null;
            var names=new[] {"默认 · 轻移开启","悬停","键盘焦点","按下","不可用","切换中","失败 · 可重试","成功 · 原速对比"};
            for(int i=0;i<names.Length;i++)
            {
                int x=20+i%2*400, y=12+i/2*124;
                form.Controls.Add(new Label { Text=names[i],AutoSize=true,Location=new Point(x,y),ForeColor=LightTheme.Secondary });
                var control=new FeelSwitch { Location=new Point(x,y+28),Size=new Size(360,64),Checked=i!=7,Font=form.Font };
                if(i==1) typeof(FeelSwitch).GetField("hovered",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(control,true);
                if(i==2) focused=control;
                if(i==3) typeof(FeelSwitch).GetField("pressed",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(control,true);
                if(i==4) control.Enabled=false;
                if(i==5) { control.Busy=true; control.Enabled=false; }
                if(i==6) control.Error="驱动未确认，请重试";
                form.Controls.Add(control);
            }
            using(var g=form.CreateGraphics()) form.Scale(new SizeF(g.DpiX/96,g.DpiY/96));
            form.Show(); Application.DoEvents(); focused.Focus();
            SendMessage(form.Handle,0x127,new IntPtr(0x30002),IntPtr.Zero); Application.DoEvents();
            using(var image=new Bitmap(form.ClientSize.Width,form.ClientSize.Height))
            { form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size)); image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"feel-switch-states.png")); }
        }
    }
}
