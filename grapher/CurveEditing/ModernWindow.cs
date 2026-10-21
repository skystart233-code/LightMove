using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace grapher.CurveEditing
{
    internal static class ModernWindow
    {
        public static GraphicsPath RoundRectangle(RectangleF bounds, float radius)
        {
            float diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
        public static void Attach(Form form)
        {
            (form as StudioForm)?.EnsureChrome();
            form.HandleCreated += (s, e) =>
            {
                try
                {
                    int dark = 0, rounded = 2;
                    DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
                    DwmSetWindowAttribute(form.Handle, 33, ref rounded, sizeof(int));
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
            };
        }
    }

    public class StudioButton : Button
    {
        private bool hovered, pressed;
        public StudioButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent?.BackColor ?? BackColor); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // ButtonBase can invalidate only its foreground during a hover or
            // enabled-state transition. Paint the entire owned buffer here;
            // OnPaintBackground alone is not a reliable erase contract.
            g.Clear(Parent?.BackColor ?? BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale; using (var display = CreateGraphics()) scale = display.DpiX / 96;
            bool primary = BackColor == LightTheme.Accent;
            var fill = !Enabled ? (BackColor == LightTheme.Page ? LightTheme.Page : LightTheme.SurfaceAlt) : pressed ? (primary ? LightTheme.AccentPressed : LightTheme.Rule) : hovered ? (primary ? LightTheme.AccentPressed : LightTheme.AccentSoft) : BackColor;
            using (var path = ModernWindow.RoundRectangle(new RectangleF(1, 1, Width - 2, Height - 2), 10 * scale))
            using (var brush = new SolidBrush(fill))
            { g.FillPath(brush, path); }
            if (Focused && ShowFocusCues)
                using (var path = ModernWindow.RoundRectangle(new RectangleF(scale, scale, Width - 2 * scale, Height - 2 * scale), 10 * scale))
                using (var pen = new Pen(LightTheme.Accent, 2 * scale)) g.DrawPath(pen, path);
            if (primary && Focused && ShowFocusCues)
                using (var path = ModernWindow.RoundRectangle(new RectangleF(4*scale,4*scale,Width-8*scale,Height-8*scale),7*scale))
                using (var pen = new Pen(LightTheme.AccentInk, scale)) g.DrawPath(pen,path);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : LightTheme.Disabled,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    // Shared vertical rhythm for toolbars, form fields and footer actions.
    // FlowLayoutPanel positions by top margins; controls of different native
    // heights need a common centre line instead of per-control pixel offsets.
    public class AlignedControlRow : FlowLayoutPanel
    {
        private bool arranging;
        protected override void OnLayout(LayoutEventArgs e)
        {
            if (arranging) return;
            arranging = true;
            try
            {
                base.OnLayout(e);
                if (WrapContents) return;
                int available = Math.Max(0, ClientSize.Height - Padding.Vertical);
                foreach (Control child in Controls)
                    if (child.Visible)
                        child.Top = Padding.Top + Math.Max(0, (available - child.Height) / 2);
            }
            finally { arranging = false; }
        }
    }

    public class EditorViewport : Panel
    {
        private bool arranging;
        public EditorViewport() { AutoScroll = true; }
        protected override void OnLayout(LayoutEventArgs e)
        {
            if (arranging) return;
            arranging = true;
            try
            {
                if (Controls.Count == 0) { base.OnLayout(e); return; }
                var content = Controls[0]; content.Dock = DockStyle.None;
                AutoScrollMinSize = content.MinimumSize;
                // ClientSize already excludes scrollbars. Reflow after scrollbars
                // settle so the old wider virtual extent cannot stick around.
                for (int pass = 0; pass < 3; pass++)
                {
                    content.Bounds = new Rectangle(AutoScrollPosition, new Size(
                        Math.Max(content.MinimumSize.Width, ClientSize.Width),
                        Math.Max(content.MinimumSize.Height, ClientSize.Height)));
                    base.OnLayout(e);
                }
            }
            finally { arranging = false; }
        }
    }

    public class StudioComboBox : ComboBox
    {
        public StudioComboBox() { DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (var g = CreateGraphics()) ItemHeight = (int)Math.Ceiling(g.MeasureString("中Aa", Font).Height) + 4;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? LightTheme.AccentSoft : BackColor))
                e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index >= 0)
                TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height), ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
        }
    }

    public class StudioCheckBox : CheckBox
    {
        public StudioCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Parent?.BackColor ?? BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var display = CreateGraphics())
            {
                float scale = display.DpiX / 96, size = 30 * scale, height = 18 * scale;
                var box = new RectangleF(2 * scale, (Height - height) / 2, size, height);
                using (var path = ModernWindow.RoundRectangle(box, height / 2))
                using (var brush = new SolidBrush(!Enabled ? LightTheme.Rule : Checked ? LightTheme.Accent : LightTheme.Baseline))
                { g.FillPath(brush, path); }
                using (var brush = new SolidBrush(LightTheme.Surface))
                    g.FillEllipse(brush, box.X + (Checked ? 14 : 2) * scale, box.Y + 2 * scale, 14 * scale, 14 * scale);
                if (Focused && ShowFocusCues)
                    using (var path = ModernWindow.RoundRectangle(new RectangleF(box.X - scale, box.Y - scale, box.Width + 2 * scale, box.Height + 2 * scale), height / 2))
                    using (var pen = new Pen(LightTheme.Accent, scale)) g.DrawPath(pen, path);
                TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(size + 10 * scale), 0, Width - (int)(size + 10 * scale), Height), Enabled ? ForeColor : LightTheme.Disabled,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    public class RoundedCard : Panel
    {
        public Color FillColor { get; set; } = LightTheme.Surface;
        public bool ClipChildren { get; set; }
        public RoundedCard() { DoubleBuffered = true; ResizeRedraw = true; }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!ClipChildren || Width < 2 || Height < 2) return;
            using (var display = CreateGraphics())
            using (var path = ModernWindow.RoundRectangle(ClientRectangle, LightTheme.Radius * display.DpiX / 96))
            { var previous = Region; Region = new Region(path); previous?.Dispose(); }
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (var path = ModernWindow.RoundRectangle(bounds, LightTheme.Radius * e.Graphics.DpiX / 96))
            using (var fill = new SolidBrush(FillColor))
            {
                e.Graphics.FillPath(fill, path);
            }
        }
    }
}
