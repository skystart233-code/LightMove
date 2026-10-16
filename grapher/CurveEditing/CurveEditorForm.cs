using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace grapher.CurveEditing
{
    public sealed class CurveEditorState
    {
        public string ProfileSignature { get; set; }
        public List<CurvePoint> X { get; set; }
        public List<CurvePoint> Y { get; set; }
    }

    public sealed class CurveEditorForm : Form
    {
        private Profile source;
        private readonly Profile originalProfile;
        private readonly Func<Profile, Task> applyProfile;
        private readonly string statePath;
        private CurveModel[] curves;
        private CurveModel[] initial;
        private readonly CurveCanvas canvas;
        private readonly ComboBox axis;
        private readonly CheckBox sync;
        private readonly NumericUpDown speed;
        private readonly NumericUpDown multiplier;
        private readonly NumericUpDown viewSpeed;
        private readonly Label status;
        private readonly Button apply;
        private readonly Button restore;
        private bool updating, dirty, applying;
        private bool[] changed = new bool[2];

        public CurveEditorForm(Profile profile, Func<Profile, Task> applyProfile, string statePath)
        {
            source = CurveProfile.Copy(profile); originalProfile = CurveProfile.Copy(profile);
            this.applyProfile = applyProfile; this.statePath = statePath;
            curves = new[] { CurveProfile.Import(source.argsX), CurveProfile.Import(source.argsY) };
            try
            {
                if (File.Exists(statePath))
                {
                    var saved = JsonConvert.DeserializeObject<CurveEditorState>(File.ReadAllText(statePath));
                    if (saved.ProfileSignature == CurveProfile.Signature(source))
                        curves = new[] { new CurveModel(saved.X), new CurveModel(saved.Y) };
                }
            }
            catch { /* An old or incomplete editor cache must not prevent editing. */ }
            initial = curves.Select(c => c.Copy()).ToArray();
            Text = "拖点调曲线 · Raw Accel"; Name = "CurveEditorForm";
            BackColor = Color.FromArgb(26, 28, 32); ForeColor = Color.FromArgb(227, 231, 239);
            Font = new Font("Microsoft YaHei UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(1000, 720);
            MinimumSize = new Size(900, 660); StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
            foreach (var height in new float[] { 74, 43, 0, 55, 30, 55 })
                layout.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, height));
            Controls.Add(layout);
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            heading.Controls.Add(new Label { Text = "用控制点调整鼠标手感", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true, Margin = Padding.Empty });
            heading.Controls.Add(new Label { Text = "慢速精调，快速跨屏。拖动圆点预览，点击「应用曲线」后生效。", AutoSize = true, ForeColor = Color.FromArgb(163, 171, 185), Margin = Padding.Empty });
            layout.Controls.Add(heading, 0, 0);

            var toolbar = Flow();
            toolbar.Controls.Add(LabelFor("编辑轴向"));
            axis = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, FlatStyle = FlatStyle.Flat };
            axis.Items.AddRange(new object[] { "整体 / X", "纵向 / Y" }); axis.SelectedIndex = 0;
            toolbar.Controls.Add(axis);
            sync = new CheckBox { Text = "同步 X / Y", Checked = source.argsX.IsEquivalentTo(source.argsY), AutoSize = true, Margin = new Padding(16, 6, 20, 0) };
            toolbar.Controls.Add(sync);
            toolbar.Controls.Add(LabelFor("横轴上限"));
            viewSpeed = Number(10, 10000, 0, 85); viewSpeed.Increment = 10;
            toolbar.Controls.Add(viewSpeed);
            var fit = ButtonFor("适合窗口", 100); toolbar.Controls.Add(fit);
            layout.Controls.Add(toolbar, 0, 1);

            canvas = new CurveCanvas { Dock = DockStyle.Fill, Margin = Padding.Empty };
            layout.Controls.Add(canvas, 0, 2);

            var coordinates = Flow(); coordinates.Padding = new Padding(0, 10, 0, 0);
            coordinates.Controls.Add(LabelFor("选中点   速度")); speed = Number(0, 10000, 2, 95); coordinates.Controls.Add(speed);
            coordinates.Controls.Add(LabelFor("倍率")); multiplier = Number(.1m, 16, 3, 95); multiplier.Increment = .05m; coordinates.Controls.Add(multiplier);
            var delete = ButtonFor("删除此点", 105); coordinates.Controls.Add(delete);
            coordinates.Controls.Add(new Label { Text = "双击添加点 · 右键删点 · 最右点决定高速倍率", AutoSize = true, ForeColor = Color.FromArgb(163, 171, 185), Margin = new Padding(18, 7, 0, 0) });
            layout.Controls.Add(coordinates, 0, 3);
            status = new Label { Dock = DockStyle.Fill, Text = "蓝线：待应用曲线     灰虚线：当前已生效曲线", ForeColor = Color.FromArgb(163, 171, 185), Margin = Padding.Empty };
            layout.Controls.Add(status, 0, 4);
            var buttons = Flow(); buttons.FlowDirection = FlowDirection.RightToLeft; buttons.Padding = new Padding(0, 8, 0, 0);
            apply = ButtonFor("应用曲线", 128); apply.BackColor = Color.FromArgb(69, 154, 181); apply.ForeColor = Color.White; apply.Enabled = false;
            var close = ButtonFor("关闭", 100); close.DialogResult = DialogResult.Cancel;
            var reset = ButtonFor("重置本次编辑", 140);
            restore = ButtonFor("恢复原曲线", 130); restore.Enabled = false;
            buttons.Controls.AddRange(new Control[] { apply, close, reset, restore }); layout.Controls.Add(buttons, 0, 5);
            CancelButton = close;

            canvas.CurveChanged += (s, e) => { changed[axis.SelectedIndex] = true; dirty = true; apply.Enabled = true; status.Text = "预览中，尚未改变鼠标手感。蓝线为实际应用后的曲线。"; };
            canvas.SelectionChanged += (s, e) => UpdateCoordinates();
            axis.SelectedIndexChanged += (s, e) => SelectAxis();
            sync.CheckedChanged += (s, e) => { axis.Enabled = !sync.Checked; dirty = sync.Checked || changed.Any(c => c); apply.Enabled = dirty; };
            viewSpeed.ValueChanged += (s, e) =>
            {
                if (updating) return;
                canvas.MaxX = Math.Max((double)viewSpeed.Value, canvas.Model.Points.Last().X);
                viewSpeed.Value = (decimal)canvas.MaxX; canvas.Invalidate();
            };
            fit.Click += (s, e) => Fit();
            speed.ValueChanged += CoordinateChanged; multiplier.ValueChanged += CoordinateChanged;
            delete.Click += (s, e) => canvas.DeleteSelected();
            reset.Click += (s, e) => { curves = initial.Select(c => c.Copy()).ToArray(); changed = new bool[2]; dirty = false; apply.Enabled = false; SelectAxis(); status.Text = "已撤销本次拖动；鼠标设置没有改变。"; };
            apply.Click += async (s, e) => await Apply(false);
            restore.Click += async (s, e) => await Apply(true);
            FormClosing += (s, e) => { if (applying) e.Cancel = true; };
            axis.Enabled = !sync.Checked;
            SelectAxis();
        }
        private FlowLayoutPanel Flow() => new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        private Label LabelFor(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(0, 7, 10, 0) };
        private NumericUpDown Number(decimal min, decimal max, int decimals, int width) => new NumericUpDown
        {
            Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = width,
            BackColor = Color.FromArgb(43, 46, 52), ForeColor = ForeColor, BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 3, 14, 0)
        };
        private Button ButtonFor(string text, int width)
        {
            var button = new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 46, 52), ForeColor = ForeColor, Margin = new Padding(0, 0, 10, 0), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = Color.FromArgb(73, 79, 90); return button;
        }
        private void SelectAxis()
        {
            canvas.Model = curves[axis.SelectedIndex];
            canvas.Baseline = CurveProfile.Sampler(axis.SelectedIndex == 0 ? source.argsX : source.argsY);
            canvas.Selected = 0; Fit(); UpdateCoordinates();
        }
        private void Fit()
        {
            canvas.MaxX = Math.Max(10, Math.Ceiling(canvas.Model.Points.Last().X / 10) * 10);
            canvas.MaxY = Math.Min(16, Math.Max(2, Math.Ceiling(canvas.Model.Points.Max(p => p.Y) * 1.2 * 2) / 2));
            updating = true; viewSpeed.Value = (decimal)canvas.MaxX; updating = false;
            canvas.Invalidate();
        }
        private void UpdateCoordinates()
        {
            updating = true;
            var point = canvas.Model.Points[canvas.Selected];
            speed.Value = (decimal)point.X; multiplier.Value = (decimal)point.Y;
            speed.Enabled = canvas.Selected != 0;
            updating = false;
        }
        private void CoordinateChanged(object sender, EventArgs e)
        {
            if (updating) return;
            canvas.MaxX = Math.Max(canvas.MaxX, Math.Ceiling((double)speed.Value / 10) * 10);
            canvas.MaxY = Math.Max(canvas.MaxY, Math.Min(16, Math.Ceiling((double)multiplier.Value * 1.1 * 2) / 2));
            canvas.ChangeSelected((double)speed.Value, (double)multiplier.Value);
        }
        public Profile PreviewProfile()
        {
            var profile = CurveProfile.Copy(source);
            var xCurve = sync.Checked ? curves[axis.SelectedIndex] : curves[0];
            var yCurve = sync.Checked ? curves[axis.SelectedIndex] : curves[1];
            if (sync.Checked || changed[0]) profile.argsX = CurveProfile.Export(xCurve, profile.argsX);
            if (sync.Checked || changed[1]) profile.argsY = CurveProfile.Export(yCurve, profile.argsY);
            return profile;
        }
        private async Task Apply(bool restoreOriginal)
        {
            if (applying || (!dirty && !restoreOriginal)) return;
            applying = true; Enabled = false; UseWaitCursor = true;
            status.Text = "正在应用并核对驱动设置…";
            try
            {
                var profile = restoreOriginal ? CurveProfile.Copy(originalProfile) : PreviewProfile();
                await applyProfile(profile);
                if (restoreOriginal)
                {
                    curves = new[] { CurveProfile.Import(profile.argsX), CurveProfile.Import(profile.argsY) };
                    sync.Checked = profile.argsX.IsEquivalentTo(profile.argsY);
                    if (File.Exists(statePath)) File.Delete(statePath);
                    restore.Enabled = false;
                }
                else
                {
                    if (sync.Checked) curves = new[] { curves[axis.SelectedIndex].Copy(), curves[axis.SelectedIndex].Copy() };
                    var state = new CurveEditorState { ProfileSignature = CurveProfile.Signature(profile), X = curves[0].Points, Y = curves[1].Points };
                    try { File.WriteAllText(statePath, JsonConvert.SerializeObject(state, Formatting.Indented)); }
                    catch { /* The driver and settings.json have already been verified and saved. */ }
                    restore.Enabled = true;
                }
                source = CurveProfile.Copy(profile); initial = curves.Select(c => c.Copy()).ToArray();
                changed = new bool[2]; dirty = false; apply.Enabled = false; SelectAxis();
                status.Text = restoreOriginal ? "已恢复打开编辑器之前的原曲线。" : "已应用并保存。现在移动鼠标即可体验；可以继续拖点微调。";
            }
            catch (Exception ex)
            {
                status.Text = "应用未完成，请查看提示。";
                MessageBox.Show(this, ex.Message, "无法应用曲线", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { applying = false; Enabled = true; UseWaitCursor = false; }
        }
    }
}
