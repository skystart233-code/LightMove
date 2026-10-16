using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace grapher.CurveEditing
{
    public class CurveCanvas : Control
    {
        public CurveModel Model { get; set; }
        public Func<double, double> Baseline { get; set; }
        public double MaxX { get; set; } = 60;
        public double MaxY { get; set; } = 2.5;
        public int Selected { get; set; }
        private bool dragging;
        public event EventHandler CurveChanged;
        public event EventHandler SelectionChanged;
        public CurveCanvas()
        {
            DoubleBuffered = true; ResizeRedraw = true; TabStop = true;
            BackColor = Color.FromArgb(34, 36, 40); ForeColor = Color.FromArgb(218, 221, 228);
            AccessibleName = "可拖动控制点的鼠标加速曲线";
            SetStyle(ControlStyles.Selectable, true);
        }
        public RectangleF Plot => new RectangleF(65, 20, Math.Max(40, Width - 92), Math.Max(40, Height - 83));
        public PointF Pixel(double x, double y)
        {
            var r = Plot; return new PointF((float)(r.Left + x / MaxX * r.Width), (float)(r.Bottom - y / MaxY * r.Height));
        }
        private CurvePoint Value(Point p)
        {
            var r = Plot; return new CurvePoint((p.X - r.Left) / r.Width * MaxX, (r.Bottom - p.Y) / r.Height * MaxY);
        }
        private int Hit(Point p)
        {
            if (Model == null) return -1;
            for (int i = Model.Points.Count - 1; i >= 0; i--)
            {
                var v = Pixel(Model.Points[i].X, Model.Points[i].Y);
                if (Math.Pow(v.X - p.X, 2) + Math.Pow(v.Y - p.Y, 2) < 169) return i;
            }
            return -1;
        }
        public void ChangeSelected(double x, double y)
        {
            Model.Move(Selected, x, y, MaxX, MaxY);
            CurveChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        public void DeleteSelected()
        {
            if (Model.Delete(Selected))
            {
                Selected = Math.Min(Selected, Model.Points.Count - 1);
                CurveChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            int hit = Hit(e.Location);
            if (hit < 0) return;
            Selected = hit; SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            if (e.Button == MouseButtons.Right) { DeleteSelected(); return; }
            if (e.Button == MouseButtons.Left) { dragging = true; Capture = true; Cursor = Cursors.Hand; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                var value = Value(e.Location); ChangeSelected(value.X, value.Y);
            }
            else Cursor = Hit(e.Location) >= 0 ? Cursors.Hand : Cursors.Cross;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); dragging = false; Capture = false;
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || !Plot.Contains(e.Location) || Hit(e.Location) >= 0) return;
            var value = Value(e.Location); int index = Model.Add(value.X, value.Y);
            if (index < 0) return;
            Selected = index; CurveChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; var r = Plot; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var grid = new Pen(Color.FromArgb(62, 65, 72)))
            using (var labels = new SolidBrush(Color.FromArgb(160, 166, 178)))
            using (var tickFont = new Font("Microsoft YaHei UI", 9))
            {
                for (int i = 0; i <= 6; i++)
                {
                    float x = r.Left + i * r.Width / 6;
                    g.DrawLine(grid, x, r.Top, x, r.Bottom);
                    string text = (MaxX * i / 6).ToString("0.#");
                    g.DrawString(text, tickFont, labels, x - g.MeasureString(text, tickFont).Width / 2, r.Bottom + 9);
                }
                for (int i = 0; i <= 5; i++)
                {
                    float y = r.Bottom - i * r.Height / 5;
                    g.DrawLine(grid, r.Left, y, r.Right, y);
                    string text = (MaxY * i / 5).ToString("0.##") + "×";
                    g.DrawString(text, tickFont, labels, r.Left - g.MeasureString(text, tickFont).Width - 10, y - 9);
                }
                const string axis = "鼠标移动速度（计数 / 毫秒）";
                g.DrawString(axis, tickFont, labels, r.Left + (r.Width - g.MeasureString(axis, tickFont).Width) / 2, r.Bottom + 34);
                g.DrawString("倍率", tickFont, labels, 16, 1);
            }
            if (Model == null) return;
            var state = g.Save(); g.SetClip(r);
            using (var baselinePen = new Pen(Color.FromArgb(104, 112, 125), 1.5f) { DashStyle = DashStyle.Dash })
            using (var curvePen = new Pen(Color.FromArgb(98, 204, 235), 2.8f))
            {
                var line = new PointF[361]; var baseline = new PointF[361];
                var table = Model.Export();
                for (int i = 0; i < line.Length; i++)
                {
                    double x = MaxX * i / (line.Length - 1);
                    line[i] = Pixel(x, CurveModel.Sample(table, x));
                    baseline[i] = Pixel(x, Baseline == null ? 1 : Baseline(x));
                }
                using (var fill = new GraphicsPath())
                using (var brush = new SolidBrush(Color.FromArgb(18, 98, 204, 235)))
                {
                    fill.AddLines(line); fill.AddLine(line.Last(), new PointF(r.Right, r.Bottom));
                    fill.AddLine(new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom)); fill.CloseFigure();
                    g.FillPath(brush, fill);
                }
                g.DrawLines(baselinePen, baseline); g.DrawLines(curvePen, line);
            }
            g.Restore(state);
            for (int i = 0; i < Model.Points.Count; i++)
            {
                var p = Pixel(Model.Points[i].X, Model.Points[i].Y); float size = i == Selected ? 13 : 11;
                using (var fill = new SolidBrush(BackColor))
                using (var pen = new Pen(i == Selected ? Color.White : Color.FromArgb(98, 204, 235), 2.5f))
                { g.FillEllipse(fill, p.X - size / 2, p.Y - size / 2, size, size); g.DrawEllipse(pen, p.X - size / 2, p.Y - size / 2, size, size); }
            }
        }
    }
}
