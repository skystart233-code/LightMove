using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using grapher.Models.Mouse;

namespace grapher.CurveEditing
{
    public sealed class CurveEditorState
    {
        public string ProfileSignature { get; set; }
        public List<CurvePoint> X { get; set; }
        public List<CurvePoint> Y { get; set; }
    }

    public sealed class CurveEditorForm : StudioForm
    {
        private Profile source;
        private readonly Profile originalProfile;
        private readonly Func<Profile, Task> applyProfile;
        private readonly string statePath;
        private readonly FeelComparison comparison;
        private readonly FeelSwitch feelSwitch;
        private readonly StudioCheckBox shakeCursorSwitch;
        private readonly Label shakeCursorDescription;
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
        private readonly Button deletePoint;
        private readonly Button undo, redo;
        private readonly List<EditSnapshot> undoHistory = new List<EditSnapshot>();
        private readonly List<EditSnapshot> redoHistory = new List<EditSnapshot>();
        private EditSnapshot pendingEdit;
        private bool historyReplay, initialSync;
        private sealed class EditSnapshot
        {
            public CurveModel[] Curves;
            public bool Sync;
            public int Axis, Selected;
            public double MaxX, MaxY;
        }
        private readonly Label liveState, liveNumbers, previewNumbers, pointHelp;
        private readonly Label editState;
        private readonly CheckBox snapGrid;
        private readonly ComboBox gridPrecision;
        private readonly Timer liveTimer;
        private readonly Func<LiveMouseSample> liveProvider;
        private bool updating, dirty, applying;
        private bool receivedMotion;
        private Action reserveCanvasSpace;
        private bool[] changed = new bool[2];
        public event EventHandler DesktopSettingsRequested;
        public event EventHandler ShakeCursorToggleRequested;
        public bool ShakeCursorEnabled => shakeCursorSwitch.Checked;
        private const string ShakeCursorHelp="快速来回摇晃，指针放大约 3 倍；停下恢复，拖拽时不触发。与手感开关独立。";

        public CurveEditorForm(Profile profile, Func<Profile, Task> applyProfile, string statePath,
            Func<LiveMouseSample> liveProvider = null)
        {
            SuspendLayout();
            comparison = new FeelComparison(profile, statePath + ".comparison");
            source = CurveProfile.Copy(comparison.EnabledProfile); originalProfile = CurveProfile.Copy(source);
            this.applyProfile = applyProfile; this.statePath = statePath;
            this.liveProvider = liveProvider;
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
            Text = "轻移 LightMove"; Name = "CurveEditorForm";
            BackColor = LightTheme.Page; ForeColor = LightTheme.Ink;
            Font = new Font(LightTheme.FontFamily, 10f);
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = new Size(1000, 640);
            ClientSize = new Size(1180, 940);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ModernWindow.Attach(this);

            var viewport = new EditorViewport { Name = "ContentViewport", Dock = DockStyle.Fill };
            var layout = new TableLayoutPanel { Name = "EditorContent", Dock = DockStyle.Fill, MinimumSize = new Size(920, 800), Padding = new Padding(LightTheme.PageInset, 24, LightTheme.PageInset, 12), ColumnCount = 1, RowCount = 8 };
            foreach (var height in new float[] { 80, 0, 224, 64, 0, 64, 32, 28 })
                layout.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, height));
            layout.RowStyles[1] = new RowStyle(SizeType.Absolute, 0);
            viewport.Controls.Add(layout); Controls.Add(viewport);
            var heading = new TableLayoutPanel { Name = "EditorHeading", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 212));
            // Brand and secondary actions share one alignment row. The primary
            // switch lives beside its live feedback in the card below.
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            heading.Margin = new Padding(0, 0, 0, 16);
            var brand = new TableLayoutPanel { Name = "ProductIdentity", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty };
            brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            brand.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            brand.Controls.Add(new Label { Name = "ProductName", Text = "轻移 LightMove", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
            brand.Controls.Add(new Label { Name = "ProductTagline", Text = "线性手感，稳、准、快。", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = LightTheme.Secondary, Margin = Padding.Empty }, 0, 1);
            heading.Controls.Add(brand, 0, 0);
            editState = new Label { Text = "● 当前手感已生效", ForeColor = LightTheme.Success, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            feelSwitch = new FeelSwitch { Name = "FeelComparisonSwitch", Dock = DockStyle.Fill, MaximumSize = new Size(360, 0), Margin = new Padding(0, 0, 16, 0), Font = new Font(Font.FontFamily, 9), Checked = !comparison.Paused };
            feelSwitch.Click += async (s,e) => await ToggleComparisonAsync();
            var headerActions=Flow(); headerActions.Dock=DockStyle.Right; headerActions.Width=212;
            var help = ButtonFor("使用指南", 96); help.Name = "ToggleGuide";
            help.BackColor = LightTheme.Page; help.ForeColor = LightTheme.Accent;
            headerActions.Controls.Add(help);
            var desktop=ButtonFor("偏好设置",96); desktop.Name="DesktopSettings";
            desktop.BackColor=LightTheme.Page; desktop.ForeColor=LightTheme.Accent;
            desktop.Click+=(s,e)=>DesktopSettingsRequested?.Invoke(this,EventArgs.Empty);
            headerActions.Controls.Add(desktop); heading.Controls.Add(headerActions,1,0);
            layout.Controls.Add(heading, 0, 0);

            var guide = new Panel { Name = "UsageGuide", Dock = DockStyle.Fill, Visible = false, Margin = new Padding(0,0,0,8) };
            guide.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "大开关：立即切换轻移与原速 1×，用相同手部动作慢移、横扫，对比距离。系统鼠标设置不变。\n灰虚线与菱形：当前生效，只读。蓝线与圆点：待应用，可拖动。红点：实时手感。\nShift 对齐，Alt 自由拖动，Ctrl+Z 撤回。「应用曲线」保存预览并开启轻移。", ForeColor = LightTheme.Secondary, Margin = Padding.Empty });
            help.Click += (s,e) =>
            {
                bool show = !guide.Visible; guide.Visible = show;
                using (var display = CreateGraphics()) layout.RowStyles[1].Height = show ? 88 * display.DpiY / 96f : 0;
                help.Text = show ? "收起指南" : "使用指南";
                reserveCanvasSpace?.Invoke();
            };
            layout.Controls.Add(guide, 0, 1);

            var liveCard = new RoundedCard { Name = "LiveFeelCard", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(8, 12, LightTheme.CardInset, 12) };
            var liveLayout = new TableLayoutPanel { Name = "LiveFeelContent", Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = LightTheme.Surface, Margin = Padding.Empty };
            liveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); liveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); liveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            liveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            liveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            liveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            liveLayout.Controls.Add(feelSwitch, 0, 0); liveLayout.SetRowSpan(feelSwitch, 2);
            liveState = new Label { Text = "● 移动鼠标，观察实时红点", AutoSize = true, ForeColor = LightTheme.Live, Margin = new Padding(16, 4, 0, 0) };
            liveNumbers = new Label { Text = "1.00×", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font.FontFamily, 20, FontStyle.Bold), Margin = Padding.Empty };
            previewNumbers = new Label { Text = "1.00×", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font.FontFamily, 20, FontStyle.Bold), ForeColor = LightTheme.Preview, Margin = Padding.Empty };
            liveLayout.Controls.Add(new Label { Text = "当前倍率", AutoSize = true, ForeColor = LightTheme.Secondary, Margin = new Padding(0,4,0,0) }, 1, 0);
            liveLayout.Controls.Add(new Label { Text = "预览倍率", AutoSize = true, ForeColor = LightTheme.Secondary, Margin = new Padding(0,4,0,0) }, 2, 0);
            liveLayout.Controls.Add(liveState, 0, 2); liveLayout.Controls.Add(liveNumbers, 1, 1); liveLayout.Controls.Add(previewNumbers, 2, 1);
            editState.Margin = new Padding(0,4,0,0); editState.Font = new Font(Font.FontFamily, 9);
            liveLayout.Controls.Add(editState, 1, 2); liveLayout.SetColumnSpan(editState, 2);
            liveCard.Controls.Add(liveLayout);
            var liveSection=new TableLayoutPanel {Name="LiveAndPointerSettings",Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
            liveSection.RowStyles.Add(new RowStyle(SizeType.Absolute,128));
            liveSection.RowStyles.Add(new RowStyle(SizeType.Absolute,96));
            liveSection.Controls.Add(liveCard,0,0);
            var pointerCard=new RoundedCard {Name="ShakeCursorCard",Dock=DockStyle.Fill,Margin=new Padding(0,0,0,16),Padding=new Padding(LightTheme.CardInset,12,LightTheme.CardInset,12)};
            var pointerLayout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty,BackColor=LightTheme.Surface};
            pointerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pointerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));
            pointerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));pointerLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            pointerLayout.Controls.Add(new Label {Name="ShakeCursorTitle",Text="摇晃放大指针",Dock=DockStyle.Fill,Margin=Padding.Empty,TextAlign=ContentAlignment.MiddleLeft,Font=new Font(Font.FontFamily,11,FontStyle.Bold)},0,0);
            shakeCursorDescription=new Label {Name="ShakeCursorDescription",Text=ShakeCursorHelp,Dock=DockStyle.Fill,Margin=Padding.Empty,TextAlign=ContentAlignment.MiddleLeft,ForeColor=LightTheme.Secondary,Font=new Font(Font.FontFamily,9)};
            pointerLayout.Controls.Add(shakeCursorDescription,0,1);
            shakeCursorSwitch=new StudioCheckBox {Name="ShakeCursorSwitch",Text="已开启",Checked=true,AutoCheck=false,AutoSize=false,Dock=DockStyle.Fill,Margin=new Padding(16,0,0,0),Cursor=Cursors.Hand,AccessibleName="摇晃放大指针开关",AccessibleDescription=ShakeCursorHelp};
            shakeCursorSwitch.Click+=(s,e)=>ShakeCursorToggleRequested?.Invoke(this,EventArgs.Empty);
            pointerLayout.Controls.Add(shakeCursorSwitch,1,0);pointerLayout.SetRowSpan(shakeCursorSwitch,2);
            pointerCard.Controls.Add(pointerLayout);liveSection.Controls.Add(pointerCard,0,1);layout.Controls.Add(liveSection,0,2);

            var toolbar = Flow(); toolbar.Name = "PresetToolbar"; toolbar.Padding = new Padding(0,0,0,16);
            toolbar.Controls.Add(LabelFor("预设"));
            foreach (var preset in new[] { new { Name = "精细办公", Limit = 1.5 }, new { Name = "均衡顺手", Limit = 1.8 }, new { Name = "快速跨屏", Limit = 2.2 } })
            {
                double limit = preset.Limit; var button = ButtonFor(preset.Name, 110);
                button.Click += (s, e) => SetPreset(limit); toolbar.Controls.Add(button);
            }
            var more = ButtonFor("更多设置", 108); toolbar.Controls.Add(more);
            var presets = ButtonFor("我的预设",104); presets.Name="UserPresets"; toolbar.Controls.Add(presets);
            presets.Click+=(s,e)=>OpenUserPresets();
            undo = ButtonFor("撤回", 80); undo.Name = "UndoCurve"; undo.AccessibleDescription = "Ctrl+Z 撤回上一次调整"; undo.Enabled = false;
            redo = ButtonFor("重做", 80); redo.Name = "RedoCurve"; redo.AccessibleDescription = "Ctrl+Y 重做上一次调整"; redo.Enabled = false;
            toolbar.Controls.Add(undo); toolbar.Controls.Add(redo);
            layout.Controls.Add(toolbar, 0, 3);

            var options = Flow();
            options.Controls.Add(LabelFor("调节方向"));
            axis = new StudioComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115, BackColor = LightTheme.SurfaceAlt, ForeColor = ForeColor };
            axis.Items.AddRange(new object[] { "整体 / 水平", "垂直方向" }); axis.SelectedIndex = 0;
            options.Controls.Add(axis);
            sync = new StudioCheckBox { Text = "两个方向一起调", Checked = source.argsX.IsEquivalentTo(source.argsY), AutoSize = false, Width = 184, Height = 30, Margin = new Padding(16, 2, 16, 0) };
            initialSync = sync.Checked;
            options.Controls.Add(sync);
            options.Controls.Add(LabelFor("显示速度范围"));
            viewSpeed = Number(10, 10000, 0, 85); viewSpeed.Increment = 10;
            options.Controls.Add(viewSpeed);
            var fit = ButtonFor("显示完整曲线", 140); options.Controls.Add(fit);
            // More settings are an inline expansion, hidden from the first-use path.
            var optionsHost = new Panel { Dock = DockStyle.Top, Height = 0, Visible = false, Margin = Padding.Empty };
            optionsHost.Controls.Add(options);

            canvas = new CurveCanvas { Dock = DockStyle.Fill, Margin = Padding.Empty };
            var editTools = Flow(); editTools.Name = "CurveEditTools"; editTools.Dock = DockStyle.Top; editTools.Height = 56;
            editTools.Padding = new Padding(LightTheme.CardInset, 8, LightTheme.CardInset, 8); editTools.BackColor = canvas.BackColor;
            snapGrid = new StudioCheckBox { Name = "SnapGrid", Text = "网格吸附", AutoSize = false, Width = 124, Height = LightTheme.ControlHeight, Checked = true, Margin = new Padding(0, 0, 16, 0) };
            gridPrecision = new StudioComboBox { Name = "GridPrecision", DropDownStyle = ComboBoxStyle.DropDownList, Width = 215, BackColor = LightTheme.SurfaceAlt, ForeColor = ForeColor, Margin = new Padding(0, 1, 16, 0) };
            gridPrecision.Items.AddRange(new object[] { "标准 · 速度 1 / 倍率 0.1", "精细 · 速度 0.5 / 倍率 0.05" }); gridPrecision.SelectedIndex = 0;
            editTools.Controls.Add(snapGrid); editTools.Controls.Add(gridPrecision);
            editTools.Controls.Add(new Label { Text = "Shift 对齐  ·  Alt 自由拖动", AutoSize = true, ForeColor = LightTheme.Muted, Margin = new Padding(0, 6, 0, 0) });
            snapGrid.CheckedChanged += (s, e) => { canvas.SnapToGrid = snapGrid.Checked; canvas.Invalidate(); };
            gridPrecision.SelectedIndexChanged += (s, e) => { canvas.GridSpeedStep = gridPrecision.SelectedIndex == 0 ? 1 : .5; canvas.GridMultiplierStep = gridPrecision.SelectedIndex == 0 ? .1 : .05; canvas.Invalidate(); };
            var graphHost = new RoundedCard { Dock = DockStyle.Fill, Margin = Padding.Empty, ClipChildren = true };
            graphHost.Controls.Add(canvas); graphHost.Controls.Add(optionsHost); graphHost.Controls.Add(editTools);
            more.Click += (s, e) => { optionsHost.Visible = !optionsHost.Visible; optionsHost.Height = optionsHost.Visible ? fit.Height + fit.Margin.Vertical + 8 : 0; more.Text = optionsHost.Visible ? "收起设置" : "更多设置"; reserveCanvasSpace?.Invoke(); };
            layout.Controls.Add(graphHost, 0, 4);

            var coordinates = Flow(); coordinates.Name = "PointCoordinates"; coordinates.Padding = new Padding(0, 16, 0, 8);
            coordinates.Controls.Add(LabelFor("速度")); speed = Number(0, 10000, 2, 105); coordinates.Controls.Add(NumberField(speed));
            coordinates.Controls.Add(LabelFor("倍率")); multiplier = Number(.1m, 16, 3, 105); multiplier.Increment = .05m; coordinates.Controls.Add(NumberField(multiplier));
            deletePoint = ButtonFor("删除点", 90); coordinates.Controls.Add(deletePoint);
            pointHelp = new Label { Text = "", AutoSize = true, ForeColor = LightTheme.Secondary, Margin = new Padding(12, 7, 0, 0) };
            coordinates.Controls.Add(pointHelp); layout.Controls.Add(coordinates, 0, 5);
            status = new Label { Dock = DockStyle.Fill, Text = comparison.RecoveryError ?? "点击上方开关对比原速与轻移；拖动蓝点只预览，满意后再应用。", ForeColor = LightTheme.Secondary, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(status, 0, 6);
            layout.Controls.Add(new Label { Text = "双击加点  ·  右键删点  ·  Ctrl+Z 撤回  ·  Ctrl+Y 重做", AutoSize = true, ForeColor = LightTheme.Muted, Margin = Padding.Empty }, 0, 7);
            var footer = new TableLayoutPanel { Name = "EditorActions", Dock = DockStyle.Bottom, Height = 72, ColumnCount = 2, RowCount = 1, Padding = new Padding(LightTheme.PageInset,16,LightTheme.PageInset,16), Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var buttons = Flow(); buttons.FlowDirection = FlowDirection.RightToLeft;
            apply = ButtonFor("应用曲线", 145); apply.BackColor = LightTheme.Accent; apply.ForeColor = LightTheme.AccentInk; apply.Enabled = false;
            var close = ButtonFor("关闭窗口", 100); close.Name="CloseWindow"; close.DialogResult = DialogResult.Cancel;
            var reset = ButtonFor("清空预览", 120); reset.Name = "ClearPreview";
            restore = ButtonFor("恢复原曲线", 125); restore.Enabled = false;
            foreach (var secondary in new[] { close, reset, restore }) { secondary.BackColor = LightTheme.Page; secondary.ForeColor = LightTheme.Secondary; }
            foreach (var action in new[] { apply, close, reset, restore }) action.Margin = new Padding(8,0,0,0);
            buttons.Controls.AddRange(new Control[] { apply, close, reset, restore }); footer.Controls.Add(buttons,1,0); Controls.Add(footer);
            var about = ButtonFor("关于", 80); about.BackColor = LightTheme.Page; about.ForeColor = LightTheme.Secondary;
            about.Click += (s, e) => MessageBox.Show(this,
                "轻移 LightMove · 社区预览版\n线性手感，稳、准、快。\n\n为减少反复挪动鼠标、兼顾精细定位而设计。\n基于 Raw Accel v1.7.1，保留其官方驱动与 MIT 许可证。\n\n上游：github.com/RawAccelOfficial/rawaccel\n项目：github.com/skystart233-code/LightMove（独立仓库，基于上游）\n\n软件调整操作手感，不提供健康效果承诺。", "关于轻移", MessageBoxButtons.OK, MessageBoxIcon.Information);
            about.Anchor = AnchorStyles.Left; about.Margin = Padding.Empty; footer.Controls.Add(about,0,0);
            CancelButton = close;

            canvas.CurveChanged += (s, e) => { changed[axis.SelectedIndex] = true; dirty = true; apply.Enabled = true; status.Text = "预览中，尚未改变鼠标手感。蓝线为实际应用后的曲线。"; UpdateLiveNumbers(); };
            canvas.SelectionChanged += (s, e) => UpdateCoordinates();
            canvas.EditStarted += (s, e) => BeginCurveEdit();
            canvas.EditCompleted += (s, e) => EndCurveEdit();
            axis.SelectedIndexChanged += (s, e) => { if (!historyReplay) SelectAxis(); };
            sync.CheckedChanged += (s, e) =>
            {
                axis.Enabled = !sync.Checked;
                if (historyReplay || applying) return;
                // CheckedChanged runs after the value changes; capture the old value.
                BeginCurveEdit(); pendingEdit.Sync = !sync.Checked; EndCurveEdit();
            };
            viewSpeed.ValueChanged += (s, e) =>
            {
                if (updating) return;
                canvas.MaxX = Math.Max((double)viewSpeed.Value, canvas.Model.Points.Last().X);
                viewSpeed.Value = (decimal)canvas.MaxX; canvas.Invalidate();
            };
            fit.Click += (s, e) => Fit();
            speed.ValueChanged += CoordinateChanged; multiplier.ValueChanged += CoordinateChanged;
            deletePoint.Click += (s, e) => canvas.DeleteSelected();
            reset.Click += (s, e) =>
            {
                canvas.FinishEditing(); BeginCurveEdit();
                curves = initial.Select(c => c.Copy()).ToArray();
                historyReplay = true; sync.Checked = initialSync; historyReplay = false;
                SelectAxis(); EndCurveEdit(); status.Text = "已清空未应用预览；可点撤回恢复。当前手感保持不变。";
            };
            undo.Click += (s, e) => UndoCurve(); redo.Click += (s, e) => RedoCurve();
            apply.Click += async (s, e) => await Apply(false);
            restore.Click += async (s, e) => await Apply(true);
            close.Click += (s,e)=>Close();
            FormClosing += (s, e) => { if (applying) e.Cancel = true; };
            axis.Enabled = !sync.Checked;
            SelectAxis();
            UpdateLiveNumbers();
            liveTimer = new Timer { Interval = 33 };
            liveTimer.Tick += (s, e) => UpdateLiveSample(this.liveProvider?.Invoke(), DateTime.UtcNow);
            Shown += (s, e) => liveTimer.Start();
            FormClosed += (s, e) => liveTimer.Stop();
            // Every dimension is authored at 96 DPI. Legacy Framework's DeviceDpi may
            // remain 96 even in a DPI-aware process; scale once using the actual display
            // graphics, including table rows and buttons, while fonts keep point units.
            float editorScale;
            using (var display = CreateGraphics())
            { editorScale=display.DpiX/96f; Scale(new SizeF(editorScale, display.DpiY/96f)); }
            bool reserving=false;
            reserveCanvasSpace=()=>
            {
                if(reserving || canvas.Width<=0)return;
                reserving=true;
                try
                {
                    // Match the comfortable plot height of the first published
                    // desktop layout. Do not derive a huge height from width.
                    int graphHeight=(int)Math.Ceiling((260+152)*editorScale)+editTools.Height+(optionsHost.Visible?optionsHost.Height:0);
                    int fixedHeight=layout.Padding.Vertical+(int)Math.Ceiling(layout.RowStyles.Cast<RowStyle>().Where((r,i)=>i!=4 && r.SizeType==SizeType.Absolute).Sum(r=>r.Height));
                    int height=Math.Max((int)(800*editorScale),fixedHeight+graphHeight);
                    if(layout.MinimumSize.Height!=height)
                    {layout.MinimumSize=new Size(layout.MinimumSize.Width,height);viewport.PerformLayout();}
                }
                finally{reserving=false;}
            };
            viewport.SizeChanged+=(s,e)=>reserveCanvasSpace();
            canvas.SizeChanged+=(s,e)=>reserveCanvasSpace();
            Shown+=(s,e)=>reserveCanvasSpace();
            ResumeLayout(true);
            reserveCanvasSpace();
        }
        public void SetShakeCursorState(bool enabled,string message=null)
        {
            shakeCursorSwitch.Checked=enabled;shakeCursorSwitch.Text=enabled ? "已开启" : "已关闭";
            shakeCursorDescription.Text=message ?? ShakeCursorHelp;
            shakeCursorDescription.ForeColor=message==null ? LightTheme.Secondary : LightTheme.Live;
            shakeCursorSwitch.Invalidate();
        }
        public UserCurvePreset CaptureUserPreset()
        {
            var x=sync.Checked?curves[axis.SelectedIndex]:curves[0];
            var y=sync.Checked?curves[axis.SelectedIndex]:curves[1];
            return new UserCurvePreset {Id="draft",Name="我的手感",Linked=sync.Checked,X=x.Copy().Points,Y=y.Copy().Points};
        }
        public void LoadUserPreset(UserCurvePreset preset)
        {
            if(applying)return;
            var x=new CurveModel(preset.X);var y=new CurveModel(preset.Y);
            canvas.FinishEditing();BeginCurveEdit();
            curves=new[]{x,y};historyReplay=true;
            sync.Checked=preset.Linked;axis.SelectedIndex=0;axis.Enabled=!sync.Checked;historyReplay=false;
            SelectAxis();RecomputePreview();EndCurveEdit();
            status.Text="已载入「"+preset.Name+"」预览；点击应用后生效，可撤回。";
        }
        private void OpenUserPresets()
        {
            try
            {
                canvas.FinishEditing();
                using(var manager=new UserPresetsForm(new UserPresetStore(statePath+".presets"),CaptureUserPreset))
                    if(manager.ShowDialog(this)==DialogResult.OK && manager.SelectedPreset!=null)LoadUserPreset(manager.SelectedPreset);
            }
            catch(Exception ex){MessageBox.Show(this,ex.Message,"无法打开我的预设",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width - 20), Math.Min(MinimumSize.Height, area.Height - 20));
            Size = new Size(Math.Min(Width, area.Width - 20), Math.Min(Height, area.Height - 20));
            CenterToScreen();
        }
        private FlowLayoutPanel Flow() => new AlignedControlRow { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        private Label LabelFor(string text) => new Label { Text = text, AutoSize = true, Margin = new Padding(0, 0, 12, 0) };
        private Control HelpCard(string title, string body)
        {
            var card = new RoundedCard { Dock = DockStyle.Fill, Padding = new Padding(12, 7, 10, 6), Margin = new Padding(0, 0, 10, 0) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), ForeColor = LightTheme.Ink, Margin = Padding.Empty });
            content.Controls.Add(new Label { Text = body, Dock = DockStyle.Fill, ForeColor = LightTheme.Secondary, Margin = Padding.Empty, Font = new Font(Font.FontFamily, 9.5f) });
            card.Controls.Add(content); return card;
        }
        private void SetPreset(double limit)
        {
            canvas.FinishEditing(); BeginCurveEdit();
            double range = limit - 1;
            curves[axis.SelectedIndex] = new CurveModel(new[] {
                new CurvePoint(0,1), new CurvePoint(3,1), new CurvePoint(10,1 + range * .25),
                new CurvePoint(20,1 + range * .58), new CurvePoint(40,1 + range * .9), new CurvePoint(60,limit) });
            changed[axis.SelectedIndex] = true; dirty = true; apply.Enabled = true;
            SelectAxis(); status.Text = "已载入预览。先观察红点，点击「应用曲线」后才生效。";
            EndCurveEdit();
        }
        private EditSnapshot CaptureEdit() => new EditSnapshot {
            Curves = curves.Select(c => c.Copy()).ToArray(), Sync = sync.Checked,
            Axis = axis.SelectedIndex, Selected = canvas.Selected, MaxX = canvas.MaxX, MaxY = canvas.MaxY };
        private static bool SameCurve(CurveModel a, CurveModel b) => a.Points.Count == b.Points.Count &&
            a.Points.Zip(b.Points, (x, y) => x.X == y.X && x.Y == y.Y).All(equal => equal);
        private static bool SameEdit(EditSnapshot a, EditSnapshot b) => a.Sync == b.Sync &&
            SameCurve(a.Curves[0], b.Curves[0]) && SameCurve(a.Curves[1], b.Curves[1]);
        private void BeginCurveEdit()
        {
            if (historyReplay || applying || pendingEdit != null) return;
            pendingEdit = CaptureEdit(); UpdateHistoryButtons();
        }
        private void EndCurveEdit()
        {
            if (historyReplay || pendingEdit == null) return;
            var before = pendingEdit; pendingEdit = null;
            if (!SameEdit(before, CaptureEdit())) { PushHistory(undoHistory, before); redoHistory.Clear(); }
            RecomputePreview(); UpdateHistoryButtons();
        }
        private static void PushHistory(List<EditSnapshot> history, EditSnapshot value)
        {
            history.Add(value); if (history.Count > 100) history.RemoveAt(0);
        }
        private void UpdateHistoryButtons()
        {
            undo.Enabled = !applying && pendingEdit == null && undoHistory.Count > 0;
            redo.Enabled = !applying && pendingEdit == null && redoHistory.Count > 0;
        }
        private void RecomputePreview()
        {
            for (int i = 0; i < 2; i++) changed[i] = !SameCurve(sync.Checked ? curves[axis.SelectedIndex] : curves[i], initial[i]);
            dirty = changed.Any(c => c) || sync.Checked != initialSync;
            apply.Enabled = dirty; UpdateLiveNumbers();
        }
        private void RestoreEdit(EditSnapshot snapshot)
        {
            historyReplay = true;
            try
            {
                curves = snapshot.Curves.Select(c => c.Copy()).ToArray();
                sync.Checked = snapshot.Sync; axis.SelectedIndex = snapshot.Axis; axis.Enabled = !sync.Checked;
                canvas.Model = curves[axis.SelectedIndex];
                UpdateAppliedReference();
                canvas.Selected = Math.Max(0, Math.Min(snapshot.Selected, canvas.Model.Points.Count - 1));
                canvas.MaxX = Math.Max(snapshot.MaxX, canvas.Model.Points.Last().X);
                canvas.MaxY = Math.Max(snapshot.MaxY, canvas.Model.Points.Max(p => p.Y));
                updating = true; viewSpeed.Value = (decimal)canvas.MaxX; updating = false;
                UpdateCoordinates(); RecomputePreview(); canvas.Invalidate();
            }
            finally { historyReplay = false; }
            UpdateHistoryButtons();
        }
        public void UndoCurve()
        {
            if (applying) return;
            canvas.FinishEditing();
            if (undoHistory.Count == 0) return;
            var previous = undoHistory[undoHistory.Count - 1]; undoHistory.RemoveAt(undoHistory.Count - 1);
            PushHistory(redoHistory, CaptureEdit()); RestoreEdit(previous);
            status.Text = "已撤回上一步。当前手感保持不变；点击应用后才改变。";
        }
        public void RedoCurve()
        {
            if (applying) return;
            canvas.FinishEditing();
            if (redoHistory.Count == 0) return;
            var next = redoHistory[redoHistory.Count - 1]; redoHistory.RemoveAt(redoHistory.Count - 1);
            PushHistory(undoHistory, CaptureEdit()); RestoreEdit(next);
            status.Text = "已重做一步。当前手感保持不变；点击应用后才改变。";
        }
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z)) { UndoCurve(); return true; }
            if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z)) { RedoCurve(); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }
        public void UpdateLiveSample(LiveMouseSample sample, DateTime now)
        {
            double age = sample == null ? double.PositiveInfinity : (now - sample.TimestampUtc).TotalMilliseconds;
            bool moving = sample != null && age >= 0 && age < 250;
            double value = moving ? (axis.SelectedIndex == 0 ? sample.SpeedX : sample.SpeedY) : 0;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) { value = 0; moving = false; }
            if (moving) receivedMotion = true;
            canvas.UpdateLivePosition(value, moving);
            liveState.Text = !receivedMotion ? "● 移动鼠标，红点会跟着你走" : !moving ? "● 已停下 · 红点回到慢移端" :
                value <= canvas.MaxX * .2 ? "● 慢速移动 · 精细定位" : value >= canvas.MaxX * .65 ? "● 快速移动 · 轻松跨屏" : "● 正常移动 · 观察红点位置";
            UpdateLiveNumbers();
        }
        private void UpdateLiveNumbers()
        {
            liveNumbers.Text = canvas.LiveAppliedMultiplier.ToString("0.00") + "×";
            previewNumbers.Text = canvas.LivePreviewMultiplier.ToString("0.00") + "×";
            previewNumbers.ForeColor = dirty ? LightTheme.Preview : LightTheme.Secondary;
            editState.Text = comparison.Paused ? (dirty ? "● 原速对比中 · 调整仍为预览" : "● 原速 1× · 调好的曲线已保留") : dirty ? "● 预览中 · 尚未应用" : "● 当前手感已生效";
            editState.ForeColor = comparison.Paused ? LightTheme.Secondary : dirty ? LightTheme.Preview : LightTheme.Success;
            feelSwitch.Checked = !comparison.Paused;
        }
        public void ReloadAppliedProfile(Profile profile)
        {
            if (comparison.Paused && CurveProfile.Signature(profile) == CurveProfile.Signature(FeelComparison.Neutral(source))) return;
            if (CurveProfile.Signature(source) == CurveProfile.Signature(profile)) return;
            comparison.Applied(profile);
            source = CurveProfile.Copy(profile);
            curves = new[] { CurveProfile.Import(source.argsX), CurveProfile.Import(source.argsY) };
            initial = curves.Select(c => c.Copy()).ToArray(); changed = new bool[2];
            undoHistory.Clear(); redoHistory.Clear(); pendingEdit = null;
            historyReplay = true;
            sync.Checked = source.argsX.IsEquivalentTo(source.argsY);
            historyReplay = false;
            initialSync = sync.Checked;
            dirty = false; apply.Enabled = false; SelectAxis();
            UpdateHistoryButtons();
            status.Text = "已载入高级面板中生效的手感。";
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) liveTimer?.Dispose();
            base.Dispose(disposing);
        }
        private NumericUpDown Number(decimal min, decimal max, int decimals, int width) => new NumericUpDown
        {
            Minimum = min, Maximum = max, DecimalPlaces = decimals, Width = width,
            BackColor = LightTheme.Surface, ForeColor = ForeColor, BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 16, 0)
        };
        private Control NumberField(NumericUpDown number)
        {
            var field = new RoundedCard { Width = number.Width + 16, Height = LightTheme.ControlHeight, Margin = new Padding(0,0,16,0), Padding = new Padding(8,0,8,0), ClipChildren = true };
            var row = new AlignedControlRow { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, BackColor = LightTheme.Surface };
            number.BorderStyle = BorderStyle.None; number.Margin = Padding.Empty;
            row.Controls.Add(number); field.Controls.Add(row); return field;
        }
        private Button ButtonFor(string text, int width)
        {
            var button = new StudioButton { Text = text, Width = width, Height = LightTheme.ControlHeight, FlatStyle = FlatStyle.Flat, BackColor = LightTheme.SurfaceAlt, ForeColor = ForeColor, Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = LightTheme.Rule; return button;
        }
        private void SelectAxis()
        {
            canvas.Model = curves[axis.SelectedIndex];
            UpdateAppliedReference();
            canvas.Selected = 0; Fit(); UpdateCoordinates();
        }
        private void UpdateAppliedReference()
        {
            var active = comparison.Paused ? FeelComparison.Neutral(source) : source;
            canvas.SetAppliedReference(CurveProfile.Sampler(axis.SelectedIndex == 0 ? active.argsX : active.argsY), initial[axis.SelectedIndex].Points);
        }
        public async Task ToggleComparisonAsync()
        {
            if (applying) return;
            canvas.FinishEditing(); applying = true; Enabled = false;
            feelSwitch.Busy = true; feelSwitch.Error = null; feelSwitch.Invalidate();
            status.Text = "正在切换并确认驱动…";
            try
            {
                await comparison.ToggleAsync(applyProfile);
                UpdateAppliedReference(); UpdateLiveNumbers();
                status.Text = comparison.Paused ? "原速对比中：慢移、横扫试试。再点开关恢复已应用手感；预览和撤回记录保留。" : "轻移已开启：再做同样的动作，感受跨屏距离。未应用的预览保持不变。";
            }
            catch (Exception ex)
            {
                feelSwitch.Error = ex.Message;
                status.Text = "切换未完成：" + ex.Message;
            }
            finally
            {
                applying = false; Enabled = true; feelSwitch.Busy = false;
                feelSwitch.Checked = !comparison.Paused; feelSwitch.Invalidate(); UpdateHistoryButtons();
            }
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
            deletePoint.Enabled = canvas.Selected > 0 && canvas.Selected < canvas.Model.Points.Count - 1;
            pointHelp.Text = canvas.Selected == 0 ? "慢移端 · 精细定位" : canvas.Selected == canvas.Model.Points.Count - 1 ?
                "高速端 · 跨屏移动" : "过渡点 · 提速位置";
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
            canvas.FinishEditing();
            var beforeApply = CaptureEdit();
            applying = true; Enabled = false; UseWaitCursor = true;
                status.Text = "正在应用…";
            try
            {
                var profile = restoreOriginal ? CurveProfile.Copy(originalProfile) : PreviewProfile();
                await applyProfile(profile);
                comparison.Applied(profile); feelSwitch.Error = null;
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
                initialSync = sync.Checked;
                if (restoreOriginal && !SameEdit(beforeApply, CaptureEdit()))
                { PushHistory(undoHistory, beforeApply); redoHistory.Clear(); }
                changed = new bool[2]; dirty = false; apply.Enabled = false; SelectAxis();
                UpdateLiveNumbers();
                status.Text = restoreOriginal ? "已恢复原曲线。" : "已应用并保存。";
            }
            catch (Exception ex)
            {
                status.Text = "应用未完成，请查看提示。";
                MessageBox.Show(this, ex.Message, "无法应用曲线", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { applying = false; Enabled = true; UseWaitCursor = false; UpdateHistoryButtons(); }
        }
    }
}
