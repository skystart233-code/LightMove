using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using grapher;
using grapher.CurveEditing;

internal static class CurveEditorTests
{
    private static int checks;
    private static string statePath;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Mouse(Control target, string name, MouseButtons button, Point point, int clicks = 1)
    {
        target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target,
            new object[] { new MouseEventArgs(button, clicks, point.X, point.Y, 0) });
    }
    private static Point At(CurveCanvas canvas, double x, double y) => Point.Round(canvas.Pixel(x, y));
    private static void Drag(CurveCanvas canvas, int index, double x, double y)
    {
        var start = canvas.Model.Points[index];
        Mouse(canvas, "OnMouseDown", MouseButtons.Left, At(canvas, start.X, start.Y));
        Mouse(canvas, "OnMouseMove", MouseButtons.Left, At(canvas, x, y));
        Mouse(canvas, "OnMouseUp", MouseButtons.Left, At(canvas, x, y));
    }
    private static Profile Natural()
    {
        var profile = new Profile(); var args = profile.argsX;
        args.mode = AccelMode.natural; args.gain = true; args.inputOffset = 3; args.decayRate = .12; args.limit = 1.8;
        profile.argsX = args; profile.argsY = args; return profile;
    }
    private static void Show(Form form)
    {
        form.ShowInTaskbar = false; form.Opacity = 0; form.Show(); Application.DoEvents();
    }
    private static void Snapshot(Form form, string name)
    {
        using (var image = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(name);
        }
    }
    private static void Pump(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Check(done(), "Asynchronous editor apply timed out");
    }
    private static void ModelTests()
    {
        var curve = new CurveModel(new[] { new CurvePoint(0, 1), new CurvePoint(3, 1), new CurvePoint(10, 1.2), new CurvePoint(25, 1.55), new CurvePoint(60, 1.8) });
        for (int i = 0; i < 600; i++)
        {
            double x = i / 10.0;
            Check(curve.Evaluate(x) >= 1 && curve.Evaluate(x) <= 1.8, "Interpolation overshoot");
            if (i > 0) Check(curve.Evaluate(x) >= curve.Evaluate(x - .1) - 1e-10, "Monotone curve reversed");
        }
        Check(curve.Evaluate(0) == 1 && curve.Evaluate(6000) == 1.8, "Endpoint plateau");
        curve.Move(2, -100, 100, 60, 3);
        Check(curve.Points[2].X > curve.Points[1].X && curve.Points[2].Y == 3, "Dragging crossed neighbor/bounds");
        curve.Move(0, 99, 1, 60, 3); Check(curve.Points[0].X == 0, "First speed must stay zero");
        int added = curve.Add(40, 1.7); Check(added >= 0 && curve.Points.Count == 6, "Add point");
        Check(curve.Add(40, 1.7) == -1, "Duplicate speed accepted");
        Check(curve.Delete(added) && !curve.Delete(0) && !curve.Delete(curve.Points.Count - 1), "Endpoint deletion");
        var many = new CurveModel(Enumerable.Range(0, 12).Select(i => new CurvePoint(i * 5, 1 + i * .05)));
        Check(many.Export().Count <= 128, "LUT exceeds safe signed-wrapper comparison length");
        Check(many.Add(2, 1) == -1, "Point limit");
        bool rejected = false;
        try { new CurveModel(new[] { new CurvePoint(0, 1), new CurvePoint(1e9, 1.5) }); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Corrupt cache can exceed numeric view controls");
        var args = CurveProfile.Export(many, new Profile().argsX);
        Check(args.IsEquivalentTo(args), "Official wrapper LUT comparison failed at maximum point count");
        var nativeProfile = new Profile { argsX = args, argsY = args };
        var config = DriverConfig.Convert(DriverConfig.FromProfile(nativeProfile).ToJSON());
        Check(config.Item2 == null, "Official wrapper rejected exported LUT");
        var sample = CurveProfile.Sampler(args);
        foreach (double speed in new double[] { .001, .1, 1, 2.5, 5, 20, 55, 100, 1000, 10000 })
            Check(Math.Abs(sample(speed) - many.ExportedValue(speed)) < .00002, "Driver curve differs from displayed LUT at " + speed);
        Check(Math.Abs(sample(10000) - many.Points.Last().Y) < .00002, "High-speed extrapolation is not capped");
        var natural = Natural(); var naturalSample = CurveProfile.Sampler(natural.argsX);
        Check(Math.Abs(naturalSample(10) - 1.2133) < .0001, "Sampler speed units changed");
        var imported = CurveProfile.Import(natural.argsX);
        Check(imported.Points[0].Y == 1, "Slow sensitivity changed on import");
        natural.outputDPI = 1600; natural.rotation = 7; natural.yxOutputDPIRatio = .8;
        var copied = CurveProfile.Copy(natural);
        copied.argsX = CurveProfile.Export(imported, copied.argsX);
        Check(copied.outputDPI == 1600 && copied.rotation == 7 && copied.yxOutputDPIRatio == .8, "Non-curve settings changed");
        using (var chart = new Chart())
        {
            chart.ChartAreas.Add(new ChartArea()); chart.Legends.Add(new Legend()); chart.Titles.Add(new Title());
            for (int i = 0; i < 4; i++) chart.Series.Add(new Series());
            ChartXY.SetupChart(chart);
            Check(!chart.ChartAreas[0].CursorX.IsUserSelectionEnabled && !chart.ChartAreas[0].CursorY.IsUserSelectionEnabled, "Drag zoom is enabled by default");
        }
    }
    private static void EditorTests()
    {
        int applied = 0; Profile last = null;
        Func<Profile, Task> callback = async profile => { await Task.Delay(30); applied++; last = CurveProfile.Copy(profile); };
        using (var form = new CurveEditorForm(Natural(), callback, statePath))
        {
            Show(form);
            var canvas = Field<CurveCanvas>(form, "canvas"); var apply = Field<Button>(form, "apply");
            Check(applied == 0 && !apply.Enabled && !File.Exists(statePath), "Opening editor changes settings");
            Snapshot(form, "curve-editor.png");
            double axisX = canvas.MaxX, axisY = canvas.MaxY;
            Drag(canvas, 3, 23, 1.65);
            Check(canvas.Selected == 3 && Math.Abs(canvas.Model.Points[3].X - 23) < .2, "Mouse drag did not move control point");
            Check(canvas.MaxX == axisX && canvas.MaxY == axisY, "Dragging unexpectedly rescaled axes");
            Check(applied == 0 && apply.Enabled, "Preview writes to driver before Apply");
            Mouse(canvas, "OnMouseDoubleClick", MouseButtons.Left, At(canvas, 35, 1.72), 2);
            Check(canvas.Model.Points.Count == 7, "Double-click does not add point");
            Mouse(canvas, "OnMouseDown", MouseButtons.Right, At(canvas, canvas.Model.Points[canvas.Selected].X, canvas.Model.Points[canvas.Selected].Y));
            Check(canvas.Model.Points.Count == 6, "Right-click does not delete point");
            Snapshot(form, "curve-editor-dragged.png");
            apply.PerformClick(); Pump(() => applied == 1 && form.Enabled);
            Check(last.argsX.mode == AccelMode.lut && !last.argsX.gain && last.argsX.IsEquivalentTo(last.argsY), "Synchronized export is not a sensitivity LUT");
            Check(File.Exists(statePath) && !apply.Enabled, "Applied points were not saved");
            var restore = Field<Button>(form, "restore"); Check(restore.Enabled, "Restore original button unavailable");
            restore.PerformClick(); Pump(() => applied == 2 && form.Enabled);
            Check(last.argsX.mode == AccelMode.natural && last.argsX.limit == 1.8 && last.argsX.inputOffset == 3, "Exact original curve was not restored");
        }
        var asymmetric = Natural(); var vertical = asymmetric.argsY; vertical.limit = 2.2; asymmetric.argsY = vertical;
        using (var form = new CurveEditorForm(asymmetric, callback, statePath))
        {
            Show(form); var canvas = Field<CurveCanvas>(form, "canvas");
            Check(!Field<CheckBox>(form, "sync").Checked, "Different axes incorrectly linked");
            Drag(canvas, 2, 12, 1.35);
            var profile = form.PreviewProfile();
            Check(profile.argsX.mode == AccelMode.lut && profile.argsY.mode == AccelMode.natural && profile.argsY.limit == 2.2, "X edit overwrote untouched Y curve");
            form.Close(); Check(applied == 2, "Closing editor applied uncommitted curve");
        }
        using (var form = new CurveEditorForm(Natural(), callback, statePath))
        {
            Show(form); var canvas = Field<CurveCanvas>(form, "canvas");
            Drag(canvas, 2, 11, 1.4); Field<Button>(form, "apply").PerformClick(); Pump(() => applied == 3 && form.Enabled);
        }
        using (var form = new CurveEditorForm(last, callback, statePath))
        {
            Show(form); var canvas = Field<CurveCanvas>(form, "canvas");
            Check(Math.Abs(canvas.Model.Points[2].X - 11) < .2, "Reopening lost editable control points");
            form.Size = form.MinimumSize; Application.DoEvents(); Snapshot(form, "curve-editor-small.png");
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (Array.IndexOf(args, "--live-no-change") >= 0)
            {
                var before = DriverConfig.GetActive();
                var beforeJson = before.ToJSON();
                using (var form = new RawAcceleration())
                {
                    var task = form.AccelGUI.Settings.ApplyGraphicalProfileAsync(CurveProfile.Copy(before.profiles[0]));
                    Pump(() => task.IsCompleted); task.GetAwaiter().GetResult();
                    Check(Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.Parse(beforeJson), DriverConfig.GetActive().ToJObject()), "Live no-change apply altered configuration");
                }
                File.WriteAllText("live-apply-result.txt", "PASS: real driver write/readback and atomic settings save succeeded; the complete active configuration stayed identical.");
                return 0;
            }
            statePath = Path.Combine(Environment.CurrentDirectory, "test-editor-state.json");
            if (File.Exists(statePath)) File.Delete(statePath);
            ModelTests(); EditorTests();
            File.WriteAllText("curve-editor-test-result.txt", "PASS: " + checks + " checks. Native LUT parity, capped tail, maximum points, drag/add/delete, fixed view, preview-only, apply/restore callbacks, per-axis preservation, control-point reload. No live driver writes.");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText("curve-editor-test-result.txt", "FAIL: " + ex); return 1; }
    }
}
