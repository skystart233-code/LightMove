using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace grapher.CurveEditing
{
    // Real window controls, not a decorative frame: native caption hit-testing
    // retains Windows move/snap, double-click maximize and border resizing.
    public class StudioForm : Form
    {
        private WindowChrome chrome;
        public StudioForm() { FormBorderStyle = FormBorderStyle.None; }
        public bool Resizable { get; set; } = true;
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetNativeStyle(IntPtr hwnd,int index);
        public bool NativeCaptionPresent => IsHandleCreated && (GetNativeStyle(Handle,-16)&0x00C00000)!=0;
        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.Style &= ~0x00C00000;
                parameters.Style |= 0x00080000; // system menu / Alt+Space
                if (Resizable) parameters.Style |= 0x00040000;
                if (MinimizeBox) parameters.Style |= 0x00020000;
                if (MaximizeBox) parameters.Style |= 0x00010000;
                return parameters;
            }
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeStyles { public int Old, New; }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo
        { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
        { public int Size; public NativeRect Monitor, Work; public uint Flags; }
        internal void EnsureChrome()
        {
            if (chrome != null) return;
            chrome = new WindowChrome(this) { Name = "WindowChrome", Dock = DockStyle.Top, Height = 44 };
            Controls.Add(chrome); chrome.SendToBack();
        }
        protected override void OnControlAdded(ControlEventArgs e)
        { base.OnControlAdded(e); chrome?.SendToBack(); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x7C && message.WParam.ToInt64() == -16) // WM_STYLECHANGING
            {
                var styles=(NativeStyles)Marshal.PtrToStructure(message.LParam,typeof(NativeStyles));
                styles.New &= ~0x00C00000;
                Marshal.StructureToPtr(styles,message.LParam,false);
            }
            if (message.Msg == 0x83) // WM_NCCALCSIZE (both RECT and NCCALCSIZE_PARAMS)
            { message.Result = IntPtr.Zero; return; }
            // Classic / non-composited desktops otherwise paint a second native
            // caption during activation, even over our client-drawn controls.
            if (message.Msg == 0x85) { message.Result=IntPtr.Zero; return; }
            if (message.Msg == 0x86) { message.Result=new IntPtr(1); return; }
            base.WndProc(ref message);
            if (message.Msg == 0x24) // WM_GETMINMAXINFO: keep taskbar visible
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (GetMonitorInfo(MonitorFromWindow(Handle, 2), ref info))
                {
                    var bounds = (MinMaxInfo)Marshal.PtrToStructure(message.LParam, typeof(MinMaxInfo));
                    bounds.MaxPosition = new NativePoint { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
                    bounds.MaxSize = new NativePoint { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
                    Marshal.StructureToPtr(bounds, message.LParam, false);
                }
            }
            if (message.Msg == 0x84 && message.Result.ToInt32() == 1) // WM_NCHITTEST
            {
                var packed = message.LParam.ToInt64();
                var point = PointToClient(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
                int border = Math.Max(6, (int)(6 * GetDpiForWindow(Handle) / 96f));
                if (WindowState == FormWindowState.Normal && Resizable)
                {
                    bool left = point.X < border, right = point.X >= ClientSize.Width - border;
                    bool top = point.Y < border, bottom = point.Y >= ClientSize.Height - border;
                    int hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1;
                    if (hit != 1) { message.Result = new IntPtr(hit); return; }
                }
                if (chrome != null && chrome.Bounds.Contains(point) && !chrome.IsButtonPoint(chrome.PointToClient(PointToScreen(point))))
                    message.Result = new IntPtr(2); // HTCAPTION: native drag/double-click/snap
            }
        }
    }

    internal sealed class WindowChrome : Panel
    {
        private readonly Form owner;
        private readonly CaptionButton minimize, maximize, close;
        public WindowChrome(Form owner)
        {
            this.owner = owner; DoubleBuffered = true;
            minimize = new CaptionButton(owner, 0) { Name = "WindowMinimize", AccessibleName = "最小化" };
            maximize = new CaptionButton(owner, 1) { Name = "WindowMaximize", AccessibleName = "最大化或还原" };
            close = new CaptionButton(owner, 2) { Name = "WindowClose", AccessibleName = "关闭窗口" };
            foreach (var button in new[] { minimize, maximize, close }) { button.Dock = DockStyle.Right; button.Width = 44; Controls.Add(button); }
            minimize.Click += (s,e) => owner.WindowState = FormWindowState.Minimized;
            maximize.Click += (s,e) => { if (owner.MaximizeBox) owner.WindowState = owner.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
            close.Click += (s,e) => owner.Close();
            owner.SizeChanged += (s,e) => maximize.Invalidate();
        }
        public bool IsButtonPoint(Point point) => new[] { minimize, maximize, close }.AnyVisibleAt(point);
        protected override void OnLayout(LayoutEventArgs e)
        {
            minimize.Visible = owner.MinimizeBox && owner.ControlBox;
            maximize.Visible = owner.MaximizeBox && owner.ControlBox;
            close.Visible = owner.ControlBox; base.OnLayout(e);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(owner.BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (var font = new Font(LightTheme.FontFamily, 9))
                TextRenderer.DrawText(e.Graphics, "轻移 LightMove", font,
                    new Rectangle(20, 0, Math.Max(1, Width - 200), Height), LightTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (message.Msg == 0x84 && message.Result.ToInt32() == 1) message.Result = new IntPtr(-1);
        }
    }
    internal static class CaptionHitTest
    {
        public static bool AnyVisibleAt(this CaptionButton[] buttons, Point point)
        { foreach (var button in buttons) if (button.Visible && button.Bounds.Contains(point)) return true; return false; }
    }
    internal sealed class CaptionButton : Button
    {
        private readonly Form owner;
        private readonly int kind;
        private bool hovered, pressed;
        public CaptionButton(Form owner, int kind)
        {
            this.owner = owner; this.kind = kind; Text = ""; FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor=false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(owner.BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics; g.Clear(owner.BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale; using(var display=CreateGraphics()) scale=display.DpiX/96;
            var bounds=new RectangleF(3*scale,5*scale,Width-6*scale,Height-10*scale);
            if(hovered || pressed)
                using(var path=ModernWindow.RoundRectangle(bounds,8*scale))
                using(var brush=new SolidBrush(kind==2 ? LightTheme.Live : LightTheme.Rule)) g.FillPath(brush,path);
            var ink=(kind==2 && (hovered || pressed)) ? LightTheme.AccentInk : LightTheme.Secondary;
            float x=Width/2f, y=Height/2f, half=4.5f*scale;
            using(var pen=new Pen(ink,1.2f*scale))
            {
                if(kind==0) g.DrawLine(pen,x-half,y+2*scale,x+half,y+2*scale);
                else if(kind==2) { g.DrawLine(pen,x-half,y-half,x+half,y+half); g.DrawLine(pen,x+half,y-half,x-half,y+half); }
                else if(owner.WindowState==FormWindowState.Maximized)
                { g.DrawRectangle(pen,x-half+2*scale,y-half,7*scale,7*scale); using(var brush=new SolidBrush(owner.BackColor)) g.FillRectangle(brush,x-half,y-half+2*scale,7*scale,7*scale); g.DrawRectangle(pen,x-half,y-half+2*scale,7*scale,7*scale); }
                else g.DrawRectangle(pen,x-half,y-half,2*half,2*half);
            }
            if(Focused && ShowFocusCues)
                using(var path=ModernWindow.RoundRectangle(bounds,8*scale))
                using(var pen=new Pen(LightTheme.Accent,1.5f*scale)) g.DrawPath(pen,path);
        }
    }
}
