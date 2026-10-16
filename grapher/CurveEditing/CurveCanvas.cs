using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace grapher.CurveEditing
{
    public class CurveCanvas : Control
    {
        private CurveModel model;
        private Func<double, double> baseline;
        private double maxX = 60, maxY = 2.5;
        private bool cacheDirty = true;
        private readonly double[] curveValues = new double[361], baselineValues = new double[361];
        private List<CurvePoint> cachedTable;
        private IReadOnlyList<CurvePoint> appliedPoints = Array.AsReadOnly(new CurvePoint[0]);
        public IReadOnlyList<CurvePoint> AppliedPoints => appliedPoints;
        public void SetAppliedReference(Func<double, double> sampler, IEnumerable<CurvePoint> points)
        {
            Baseline = sampler;
            // Keep reference points independent of the editable model. Parameter
            // curves may be approximated by the editor; sample the actual driver
            // curve at these speeds so gray markers always sit on the gray line.
            appliedPoints = Array.AsReadOnly(points.Select(p => new CurvePoint(p.X, sampler(p.X))).ToArray());
            Invalidate();
        }
        public CurveModel Model { get => model; set { model = value; cacheDirty = true; } }
        public Func<double, double> Baseline { get => baseline; set { baseline = value; cacheDirty = true; } }
        public double MaxX { get => maxX; set { maxX = value; cacheDirty = true; } }
        public double MaxY { get => maxY; set { maxY = value; cacheDirty = true; } }
        public double LiveSpeed { get; private set; }
        public bool LiveMoving { get; private set; }
        public double LiveAppliedMultiplier => Baseline == null ? 1 : Baseline(LiveSpeed);
        public double LivePreviewMultiplier => Model == null ? 1 : !cacheDirty && cachedTable != null ? CurveModel.Sample(cachedTable, LiveSpeed) : Model.ExportedValue(LiveSpeed);
        public void UpdateLivePosition(double speed, bool moving)
        {
            LiveSpeed = double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0 ? 0 : speed;
            LiveMoving = moving; Invalidate();
        }
        public int Selected { get; set; }
        public bool SnapToGrid { get; set; } = true;
        public double GridSpeedStep { get; set; } = 1;
        public double GridMultiplierStep { get; set; } = .1;
        public bool AlignmentGuideVisible { get; private set; }
        public double AlignmentGuideY { get; private set; }
        private bool dragging;
        public event EventHandler CurveChanged;
        public event EventHandler SelectionChanged;
        public event EventHandler EditStarted;
        public event EventHandler EditCompleted;
        public void FinishEditing()
        {
            if (!dragging) return;
            dragging = false; AlignmentGuideVisible = false; Capture = false;
            EditCompleted?.Invoke(this, EventArgs.Empty); Invalidate();
        }
        public CurveCanvas()
        {
            DoubleBuffered = true; ResizeRedraw = true; TabStop = true;
            BackColor = LightTheme.Surface; ForeColor = LightTheme.Ink;
            AccessibleName = "可拖动控制点的鼠标加速曲线";
            SetStyle(ControlStyles.Selectable, true);
        }
        private bool paintingLogical;
        private float DisplayScale = 1;
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (var display = CreateGraphics()) DisplayScale = display.DpiX / 96f;
        }
        public RectangleF Plot
        {
            get
            {
                float scale = paintingLogical ? 1 : DisplayScale;
                return new RectangleF(65 * scale, 40 * scale,
                    Math.Max(40 * scale, (paintingLogical ? Width / DisplayScale : Width) - 92 * scale),
                    Math.Max(40 * scale, (paintingLogical ? Height / DisplayScale : Height) - 145 * scale));
            }
        }
        public PointF Pixel(double x, double y)
        {
            var r = Plot; return new PointF((float)(r.Left + x / MaxX * r.Width), (float)(r.Bottom - y / MaxY * r.Height));
        }
        private CurvePoint Value(Point p)
        {
            var r = Plot; return new CurvePoint((p.X - r.Left) / r.Width * MaxX, (r.Bottom - p.Y) / r.Height * MaxY);
        }
        public static double SnapCoordinate(double value, double step, double min, double max)
        {
            if (step <= 0 || double.IsNaN(value) || double.IsInfinity(value)) return Math.Max(min, Math.Min(max, value));
            double first = Math.Ceiling((min - 1e-9) / step), last = Math.Floor((max + 1e-9) / step);
            if (first > last) return Math.Max(min, Math.Min(max, value));
            return Math.Round(Math.Max(first, Math.Min(last, Math.Round(value / step, MidpointRounding.AwayFromZero))) * step, 8);
        }
        public void DragSelectedTo(double x, double y, Keys modifiers)
        {
            if (Model == null || double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y)) return;
            bool free = (modifiers & Keys.Alt) != 0, align = (modifiers & Keys.Shift) != 0 && !free;
            double minX = Selected == 0 ? 0 : Model.Points[Selected - 1].X + CurveModel.Gap;
            double maxX = Selected == 0 ? 0 : Selected == Model.Points.Count - 1 ? MaxX : Model.Points[Selected + 1].X - CurveModel.Gap;
            double rawY = y;
            if (!free && (SnapToGrid || align))
            {
                x = SnapCoordinate(x, GridSpeedStep, minX, maxX);
                y = SnapCoordinate(y, GridMultiplierStep, .1, Math.Min(16, MaxY));
            }
            AlignmentGuideVisible = false;
            if (align)
            {
                var nearby = Model.Points.Where((p, i) => i != Selected && p.Y <= MaxY)
                    .OrderBy(p => Math.Abs(p.Y - rawY)).FirstOrDefault();
                if (nearby != null && Math.Abs(nearby.Y - rawY) / MaxY * Plot.Height <= 10 * DisplayScale)
                {
                    y = nearby.Y; AlignmentGuideY = y; AlignmentGuideVisible = true;
                }
            }
            ChangeSelected(x, y);
            Invalidate();
        }
        private int Hit(Point p)
        {
            if (Model == null) return -1;
            for (int i = Model.Points.Count - 1; i >= 0; i--)
            {
                var v = Pixel(Model.Points[i].X, Model.Points[i].Y);
                if (Math.Pow(v.X - p.X, 2) + Math.Pow(v.Y - p.Y, 2) < 169 * DisplayScale * DisplayScale) return i;
            }
            return -1;
        }
        public void ChangeSelected(double x, double y)
        {
            bool singleEdit = !dragging;
            if (singleEdit) EditStarted?.Invoke(this, EventArgs.Empty);
            double oldX = Model.Points[Selected].X, oldY = Model.Points[Selected].Y;
            Model.Move(Selected, x, y, MaxX, MaxY);
            if (Model.Points[Selected].X == oldX && Model.Points[Selected].Y == oldY)
            { SelectionChanged?.Invoke(this, EventArgs.Empty); if (singleEdit) EditCompleted?.Invoke(this, EventArgs.Empty); return; }
            cacheDirty = true;
            CurveChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            if (singleEdit) EditCompleted?.Invoke(this, EventArgs.Empty);
        }
        public void DeleteSelected()
        {
            FinishEditing(); EditStarted?.Invoke(this, EventArgs.Empty);
            if (Model.Delete(Selected))
            {
                Selected = Math.Min(Selected, Model.Points.Count - 1);
                cacheDirty = true;
                CurveChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            }
            EditCompleted?.Invoke(this, EventArgs.Empty);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            int hit = Hit(e.Location);
            if (hit < 0) return;
            Selected = hit; SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            if (e.Button == MouseButtons.Right) { DeleteSelected(); return; }
            if (e.Button == MouseButtons.Left) { EditStarted?.Invoke(this, EventArgs.Empty); dragging = true; Capture = true; Cursor = Cursors.Hand; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                var value = Value(e.Location); DragSelectedTo(value.X, value.Y, ModifierKeys);
            }
            else Cursor = Hit(e.Location) >= 0 ? Cursors.Hand : Cursors.Cross;
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e); FinishEditing(); AlignmentGuideVisible = false; Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) FinishEditing(); }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left || !Plot.Contains(e.Location) || Hit(e.Location) >= 0) return;
            var value = Value(e.Location);
            if (SnapToGrid && (ModifierKeys & Keys.Alt) == 0)
            { value.X = SnapCoordinate(value.X, GridSpeedStep, 0, MaxX); value.Y = SnapCoordinate(value.Y, GridMultiplierStep, .1, Math.Min(16, MaxY)); }
            FinishEditing(); EditStarted?.Invoke(this, EventArgs.Empty);
            int index = Model.Add(value.X, value.Y);
            if (index < 0) { EditCompleted?.Invoke(this, EventArgs.Empty); return; }
            cacheDirty = true;
            Selected = index; CurveChanged?.Invoke(this, EventArgs.Empty); SelectionChanged?.Invoke(this, EventArgs.Empty); Invalidate();
            EditCompleted?.Invoke(this, EventArgs.Empty);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            if (Model != null && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right || e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                var point = Model.Points[Selected]; double factor = e.Alt ? .1 : e.Shift ? 5 : 1;
                ChangeSelected(point.X + (e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Left ? -1 : 0) * GridSpeedStep * factor,
                    point.Y + (e.KeyCode == Keys.Up ? 1 : e.KeyCode == Keys.Down ? -1 : 0) * GridMultiplierStep * factor);
                e.Handled = true; e.SuppressKeyPress = true;
            }
        }
        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || base.IsInputKey(keyData);
        }
        private static double NiceStep(double value)
        {
            double unit = Math.Pow(10, Math.Floor(Math.Log10(value)));
            foreach (var factor in new[] { 1.0, 2, 2.5, 5, 10 }) if (factor * unit >= value - 1e-9) return factor * unit;
            return unit * 10;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var saved = e.Graphics.Save();
            e.Graphics.ScaleTransform(DisplayScale, DisplayScale);
            paintingLogical = true;
            try { PaintGraph(e.Graphics); }
            finally { paintingLogical = false; e.Graphics.Restore(saved); }
        }
        private void PaintGraph(Graphics g)
        {
            var r = Plot; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var grid = new Pen(LightTheme.Rule))
            using (var minor = new Pen(LightTheme.Grid))
            using (var labels = new SolidBrush(LightTheme.Secondary))
            using (var tickFont = new Font(LightTheme.FontFamily, 12, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                double minorX = GridSpeedStep, minorY = GridMultiplierStep;
                while (MaxX / minorX > 160) minorX *= 2;
                while (MaxY / minorY > 100) minorY *= 2;
                for (double value = 0; value <= MaxX + 1e-8; value += minorX)
                { float x = Pixel(value, 0).X; g.DrawLine(minor, x, r.Top, x, r.Bottom); }
                for (double value = 0; value <= MaxY + 1e-8; value += minorY)
                { float y = Pixel(0, value).Y; g.DrawLine(minor, r.Left, y, r.Right, y); }
                double majorX = NiceStep(MaxX / 6), majorY = NiceStep(MaxY / 7);
                for (double value = 0; value <= MaxX + 1e-8; value += majorX)
                {
                    float x = Pixel(value, 0).X;
                    g.DrawLine(grid, x, r.Top, x, r.Bottom);
                    string text = value.ToString("0.##");
                    g.DrawString(text, tickFont, labels, x - g.MeasureString(text, tickFont).Width / 2, r.Bottom + 9);
                }
                for (double value = 0; value <= MaxY + 1e-8; value += majorY)
                {
                    float y = Pixel(0, value).Y;
                    g.DrawLine(grid, r.Left, y, r.Right, y);
                    string text = value.ToString("0.##") + "×";
                    g.DrawString(text, tickFont, labels, r.Left - g.MeasureString(text, tickFont).Width - 10, y - 9);
                }
                const string axis = "鼠标移动速度  /  向右越快 →";
                g.DrawString(axis, tickFont, labels, r.Left + (r.Width - g.MeasureString(axis, tickFont).Width) / 2, r.Bottom + 34);
                g.DrawString("光标倍率", tickFont, labels, 9, 17);
                g.DrawString("慢移 · 精细定位", tickFont, labels, r.Left + 10, r.Top - 27);
                g.DrawString("正常移动", tickFont, labels, r.Left + r.Width * .36f, r.Top - 27);
                g.DrawString("快移 · 轻松跨屏", tickFont, labels, r.Left + r.Width * .73f, r.Top - 27);
                float legendX = r.Left, legendY = r.Bottom + 63;
                foreach (var item in new[] { new { Text = "已生效曲线 · 只读", Color = LightTheme.Baseline, Kind = 0 },
                    new { Text = "待应用曲线 · 可拖动", Color = LightTheme.Accent, Kind = 1 },
                    new { Text = "实时位置", Color = LightTheme.Live, Kind = 2 },
                    new { Text = "应用后预计", Color = LightTheme.Preview, Kind = 3 } })
                {
                    using (var pen = new Pen(item.Color, 1.6f))
                    using (var brush = new SolidBrush(item.Color))
                    {
                        if(item.Kind <= 1)
                        {
                            pen.DashStyle = item.Kind==0 ? DashStyle.Dash : DashStyle.Solid;
                            g.DrawLine(pen,legendX,legendY,legendX+22,legendY); pen.DashStyle=DashStyle.Solid;
                            if(item.Kind==0) DrawReferencePoint(g,new PointF(legendX+11,legendY),4);
                            else { using(var paper=new SolidBrush(BackColor)) g.FillEllipse(paper,legendX+7,legendY-4,8,8); g.DrawEllipse(pen,legendX+7,legendY-4,8,8); }
                        }
                        else if(item.Kind==2) g.FillEllipse(brush,legendX+7,legendY-4,8,8);
                        else g.DrawEllipse(pen,legendX+7,legendY-4,8,8);
                    }
                    g.DrawString(item.Text, tickFont, labels, legendX + 29, legendY - 8);
                    legendX += g.MeasureString(item.Text, tickFont).Width + 49;
                }
                g.DrawString("灰线与灰色菱形是当前设置，仅供对照。拖动蓝点只预览，点击「应用曲线」才生效。",
                    tickFont, labels, r.Left, r.Bottom + 83);
            }
            if (Model == null) return;
            var state = g.Save(); g.SetClip(r);
            using (var baselinePen = new Pen(LightTheme.Baseline, 1.5f) { DashStyle = DashStyle.Dash })
            using (var curvePen = new Pen(LightTheme.Accent, 2.2f))
            {
                var line = new PointF[361]; var baseline = new PointF[361];
                if (cacheDirty)
                {
                    cachedTable = Model.Export();
                    for (int i = 0; i < curveValues.Length; i++)
                    {
                        double x = MaxX * i / (curveValues.Length - 1);
                        curveValues[i] = CurveModel.Sample(cachedTable, x);
                        baselineValues[i] = Baseline == null ? 1 : Baseline(x);
                    }
                    cacheDirty = false;
                }
                for (int i = 0; i < line.Length; i++)
                {
                    double x = MaxX * i / (line.Length - 1);
                    line[i] = Pixel(x, curveValues[i]);
                    baseline[i] = Pixel(x, baselineValues[i]);
                }
                using (var fill = new GraphicsPath())
                using (var brush = new SolidBrush(Color.FromArgb(9, LightTheme.Accent)))
                {
                    fill.AddLines(line); fill.AddLine(line.Last(), new PointF(r.Right, r.Bottom));
                    fill.AddLine(new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom)); fill.CloseFigure();
                    g.FillPath(brush, fill);
                }
                g.DrawLines(baselinePen, baseline); g.DrawLines(curvePen, line);
            }
            g.Restore(state);
            foreach(var point in AppliedPoints)
            {
                if(point.X<0 || point.X>MaxX || point.Y<0 || point.Y>MaxY) continue;
                DrawReferencePoint(g,Pixel(point.X,point.Y),7);
            }
            if (AlignmentGuideVisible)
                using (var pen = new Pen(LightTheme.Preview, 1) { DashStyle = DashStyle.Dash })
                { float y = Pixel(0, AlignmentGuideY).Y; g.DrawLine(pen, r.Left, y, r.Right, y); }
            for (int i = 0; i < Model.Points.Count; i++)
            {
                var p = Pixel(Model.Points[i].X, Model.Points[i].Y); float size = i == Selected ? 13 : 11;
                using (var fill = new SolidBrush(i == Selected ? LightTheme.Accent : BackColor))
                using (var pen = new Pen(i == Selected ? LightTheme.Surface : LightTheme.Accent, 2.5f))
                { g.FillEllipse(fill, p.X - size / 2, p.Y - size / 2, size, size); g.DrawEllipse(pen, p.X - size / 2, p.Y - size / 2, size, size); }
            }
            if (dragging || Focused)
            {
                var selected = Model.Points[Selected]; var point = Pixel(selected.X, selected.Y);
                string text = (AlignmentGuideVisible ? "对齐  " : "") + selected.X.ToString("0.##") + "  /  " + selected.Y.ToString("0.00") + "×";
                using (var font = new Font(LightTheme.FontFamily, 12, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    var size = g.MeasureString(text, font);
                    var bounds = new RectangleF(Math.Max(r.Left + 2, Math.Min(r.Right - size.Width - 18, point.X + 14)),
                        Math.Max(r.Top + 2, point.Y - 36), size.Width + 16, 27);
                    using (var path = ModernWindow.RoundRectangle(bounds, 5))
                    using (var fill = new SolidBrush(LightTheme.Surface))
                    using (var border = new Pen(LightTheme.Rule))
                    using (var brush = new SolidBrush(LightTheme.Ink))
                    { g.FillPath(fill, path); g.DrawPath(border, path); g.DrawString(text, font, brush, bounds.X + 8, bounds.Y + 5); }
                }
            }
            // This marker uses the old chart's inferred INPUT speed. Raw Input has
            // already passed through the driver, so treating its deltas as input
            // directly would misleadingly plot accelerated output as hand speed.
            double shownX = Math.Min(MaxX, LiveSpeed);
            var live = Pixel(shownX, Math.Max(0, Math.Min(MaxY, LiveAppliedMultiplier)));
            var predicted = Pixel(shownX, Math.Max(0, Math.Min(MaxY, LivePreviewMultiplier)));
            using (var guide = new Pen(Color.FromArgb(45, LightTheme.Live)) { DashStyle = DashStyle.Dot })
            using (var halo = new SolidBrush(Color.FromArgb(LiveMoving ? 30 : 15, LightTheme.Live)))
            using (var red = new SolidBrush(LightTheme.Live))
            using (var border = new Pen(LightTheme.Surface, 1.6f))
            {
                if (LiveMoving) g.DrawLine(guide, live.X, r.Top, live.X, r.Bottom);
                if (Math.Abs(predicted.Y - live.Y) > 3)
                    using (var pen = new Pen(LightTheme.Preview, 2.2f))
                    { g.DrawLine(pen, live, predicted); g.DrawEllipse(pen, predicted.X - 6, predicted.Y - 6, 12, 12); }
                g.FillEllipse(halo, live.X - 15, live.Y - 15, 30, 30);
                g.FillEllipse(red, live.X - 5, live.Y - 5, 10, 10);
                g.DrawEllipse(border, live.X - 5, live.Y - 5, 10, 10);
            }
        }
        private void DrawReferencePoint(Graphics g, PointF point, float radius)
        {
            var diamond = new[] { new PointF(point.X,point.Y-radius), new PointF(point.X+radius,point.Y),
                new PointF(point.X,point.Y+radius), new PointF(point.X-radius,point.Y) };
            using(var paper=new SolidBrush(BackColor))
            using(var pen=new Pen(LightTheme.Baseline,1.8f))
            { g.FillPolygon(paper,diamond); g.DrawPolygon(pen,diamond); }
        }
    }
}
