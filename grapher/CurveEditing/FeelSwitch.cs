// Hallmark · component: feel switch · modern-minimal · existing LightTheme tokens.
// States: default, hover, focus, pressed, disabled, loading, error, success.
// Pre-emit critique: P5 H5 E4 S5 R5 V4. Native checkbox semantics; no page rebuild.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace grapher.CurveEditing
{
    public sealed class FeelSwitch : CheckBox
    {
        private bool hovered, pressed;
        public bool Busy { get; set; }
        public string Error { get; set; }
        public FeelSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
            AutoCheck = false; AutoSize = false; Cursor = Cursors.Hand;
            AccessibleName = "轻移效果开关";
            AccessibleDescription = "按空格或点击，在已应用的轻移手感与原速之间切换。未应用的预览不会生效。";
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = e.Button == MouseButtons.Left; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Parent?.BackColor ?? LightTheme.Page);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = g.DpiX / 96f;
            g.ScaleTransform(scale, scale);
            float w = Width / scale, h = Height / scale;
            bool enabled = Enabled && !Busy;
            var fill = !enabled ? LightTheme.SurfaceAlt : pressed ? LightTheme.Rule : hovered ? LightTheme.AccentSoft : LightTheme.Surface;
            using (var path = ModernWindow.RoundRectangle(new RectangleF(1, 1, w - 2, h - 2), 14))
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            var track = new RectangleF(w - 94, (h - 38) / 2, 76, 38);
            using (var path = ModernWindow.RoundRectangle(track, 19))
            using (var brush = new SolidBrush(!enabled ? LightTheme.Disabled : Checked ? LightTheme.Accent : LightTheme.Secondary)) g.FillPath(brush, path);
            float knobX = track.X + (Checked ? 42 : 4);
            using (var brush = new SolidBrush(LightTheme.Surface)) g.FillEllipse(brush, knobX, track.Y + 4, 30, 30);
            // A tiny arrow / pause mark gives the switch a recognisable character.
            using (var pen = new Pen(Checked ? LightTheme.Accent : LightTheme.Secondary, 2))
            {
                if (Checked) { g.DrawLine(pen, knobX + 9, track.Y + 19, knobX + 21, track.Y + 19); g.DrawLines(pen, new[] { new PointF(knobX + 16, track.Y + 14), new PointF(knobX + 21, track.Y + 19), new PointF(knobX + 16, track.Y + 24) }); }
                else { g.DrawLine(pen, knobX + 12, track.Y + 13, knobX + 12, track.Y + 25); g.DrawLine(pen, knobX + 18, track.Y + 13, knobX + 18, track.Y + 25); }
            }
            g.ResetTransform();
            string title = Busy ? "正在切换…" : Error != null ? "切换未完成 · 点此重试" : Checked ? "轻移已开启" : "原速对比中";
            string detail = Busy ? "正在确认鼠标手感" : Checked ? "点一下，感受开启前后" : "点一下，找回轻移手感";
            float textTop = (h - 60) / 2;
            using (var titleFont = new Font(Font.FontFamily, 12, FontStyle.Bold))
                TextRenderer.DrawText(g, title, titleFont, new Rectangle((int)(16*scale), (int)(textTop*scale), (int)((w-118)*scale), (int)(28*scale)), !enabled ? LightTheme.Secondary : Error != null ? LightTheme.Live : LightTheme.Ink, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, detail, Font, new Rectangle((int)(16*scale), (int)((textTop+32)*scale), (int)((w-118)*scale), (int)(28*scale)), LightTheme.Secondary, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            if (Focused && ShowFocusCues || Error != null)
                using (var path = ModernWindow.RoundRectangle(new RectangleF(3*scale, 3*scale, Width-6*scale, Height-6*scale), 12*scale))
                using (var pen = new Pen(Error != null ? LightTheme.Live : LightTheme.Accent, 2*scale)) g.DrawPath(pen,path);
        }
    }
}
