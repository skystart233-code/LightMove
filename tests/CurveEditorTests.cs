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
using grapher.Models.Mouse;
using grapher.Desktop;

internal static class CurveEditorTests
{
    private static int checks;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    private static extern int GetWindowStyle(IntPtr handle, int index);
    private static void CheckChromeStyle(Form form)
    { Check((GetWindowStyle(form.Handle,-16)&0x00C00000)==0,"Native caption style was reintroduced at runtime"); }
    private static int Hit(Form form, Point point)
    {
        var screen=form.PointToScreen(point);
        return SendMessage(form.Handle,0x84,IntPtr.Zero,new IntPtr((screen.Y<<16)|(screen.X&65535))).ToInt32();
    }
    private static void WindowChromeTests()
    {
        using(var form=new CurveEditorForm(Natural(),p=>Task.FromResult(0),statePath+".chrome"))
        {
            Show(form);
            // This check exercises window chrome, not live input. On a busy
            // high-DPI desktop, continuous redraw ticks can starve DoEvents.
            Field<System.Windows.Forms.Timer>(form,"liveTimer").Stop();
            CheckChromeStyle(form);
            form.Text="轻移 LightMove · 重绘验证"; form.ShowInTaskbar=true; Application.DoEvents();
            CheckChromeStyle(form);
            foreach(var button in Descendants(form).OfType<Button>().Where(b=>b is StudioButton || b.Name.StartsWith("Window")))
                using(var buffer=new Bitmap(button.Width,button.Height))
                using(var graphics=Graphics.FromImage(buffer))
                {
                    graphics.Clear(Color.Magenta);
                    button.GetType().GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{new PaintEventArgs(graphics,button.ClientRectangle)});
                    Check(buffer.GetPixel(0,0).ToArgb()==button.Parent.BackColor.ToArgb(),"Owner-drawn control leaves its background unpainted: "+button.Name+button.Text);
                }
            var chrome=form.Controls.Find("WindowChrome",true).Single();
            var viewport=form.Controls.Find("ContentViewport",true).Single();
            File.WriteAllText("window-chrome-diagnostics.txt",string.Join("\n",form.Controls.Cast<Control>().Select(c=>c.Name+" "+c.Bounds+" "+c.Visible)));
            Check(chrome.Visible && chrome.Height>=44,"Custom caption is hidden");
            Check(viewport.Top>=chrome.Bottom,"Editor covers window controls");
            Check(Hit(form,new Point(100,chrome.Height/2))==2,"Caption does not use native move/double-click hit testing");
            Check(Hit(form,new Point(1,1))==13,"Top-left resize handle lost");
            Check(Hit(form,new Point(form.ClientSize.Width-2,form.ClientSize.Height-2))==17,"Bottom-right resize handle lost");
            var max=(Button)form.Controls.Find("WindowMaximize",true).Single();
            max.PerformClick(); Application.DoEvents();
            Check(form.WindowState==FormWindowState.Maximized,"Maximize button fails");
            CheckChromeStyle(form);
            Check(Screen.FromControl(form).WorkingArea.Contains(form.Bounds),"Maximized window overlaps taskbar");
            max.PerformClick(); Application.DoEvents();
            Check(form.WindowState==FormWindowState.Normal,"Restore button fails");
            CheckChromeStyle(form);
            ((Button)form.Controls.Find("WindowMinimize",true).Single()).PerformClick(); Application.DoEvents();
            Check(form.WindowState==FormWindowState.Minimized,"Minimize button fails");
            form.WindowState=FormWindowState.Normal; Application.DoEvents();
            Snapshot(form,"lightmove-window-controls.png");
            bool cancel=true, closed=false;
            form.FormClosing+=(s,e)=>e.Cancel=cancel; form.FormClosed+=(s,e)=>closed=true;
            var close=(Button)form.Controls.Find("WindowClose",true).Single(); close.PerformClick();
            Check(!closed && !form.IsDisposed,"Custom close bypasses closing cancellation");
            cancel=false; close.PerformClick(); Check(closed,"Close button fails");
        }
    }
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
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void CheckTextFits(Form form)
    {
        foreach (var button in Descendants(form).OfType<Button>().Where(b => b.Visible))
        {
            var text = TextRenderer.MeasureText(button.Text, button.Font);
            Check(button.ClientSize.Height >= text.Height + 6 && button.ClientSize.Width >= text.Width + 6,
                "DPI clips button caption: " + button.Text);
        }
        foreach (var label in Descendants(form).OfType<Label>().Where(l => l.Visible && l.Dock == DockStyle.Fill && l.Text.Length > 0))
        {
            var text = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.ClientSize.Width, 10000), TextFormatFlags.WordBreak);
            Check(label.ClientSize.Height >= text.Height, "DPI clips label: " + label.Text);
        }
        foreach (var combo in Descendants(form).OfType<ComboBox>().Where(c => c.Visible))
        {
            var text = TextRenderer.MeasureText(combo.Text, combo.Font);
            Check(combo.ItemHeight >= text.Height, "DPI clips combo text height: " + combo.Text);
            Check(combo.ClientSize.Width >= text.Width + SystemInformation.VerticalScrollBarWidth, "DPI clips combo selection width: " + combo.Text);
        }
    }
    private static void Pump(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!done() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
        Check(done(), "Asynchronous editor apply timed out");
    }
    private static double Contrast(Color text, Color background)
    {
        Func<Color,double> luminance = color => {
            Func<byte,double> linear = v => v/255.0 <= .04045 ? v/255.0/12.92 : Math.Pow((v/255.0+.055)/1.055,2.4);
            return .2126*linear(color.R)+.7152*linear(color.G)+.0722*linear(color.B);
        };
        double a=luminance(text), b=luminance(background);
        return (Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
    }
    private static void LightThemeTests()
    {
        foreach(var paper in new[] { LightTheme.Page, LightTheme.Surface, LightTheme.SurfaceAlt })
            foreach(var ink in new[] { LightTheme.Ink, LightTheme.Secondary, LightTheme.Muted, LightTheme.Accent, LightTheme.Success, LightTheme.Live, LightTheme.Preview })
                Check(Contrast(ink,paper)>=4.5,"Light theme text contrast is below 4.5:1: "+ink+" on "+paper);
        Check(Contrast(LightTheme.AccentInk,LightTheme.Accent)>=4.5,"Primary button text contrast");
        Check(Contrast(LightTheme.Baseline,LightTheme.Surface)>=3,"Applied baseline is too faint to distinguish");
        using(var form=new CurveEditorForm(Natural(),p=>Task.FromResult(0),statePath+".light"))
        {
            Show(form); var canvas=Field<CurveCanvas>(form,"canvas");
            Check(form.BackColor==LightTheme.Page && canvas.BackColor==LightTheme.Surface,"Main window is not using the white theme");
            var guide=form.Controls.Find("UsageGuide",true).Single();
            var help=(Button)form.Controls.Find("ToggleGuide",true).Single();
            Check(!guide.Visible,"Usage guide adds clutter to the initial screen");
            help.PerformClick(); Application.DoEvents(); Check(guide.Visible,"Usage guide cannot be expanded");
            CheckTextFits(form); Snapshot(form,"lightmove-guide.png");
            help.PerformClick(); Application.DoEvents(); Check(!guide.Visible,"Usage guide cannot be collapsed");
            Check(!Field<Button>(form,"apply").Enabled,"Opening help changes the curve");
            form.Size=form.MinimumSize; Application.DoEvents();
            var viewport=(Panel)form.Controls.Find("ContentViewport",true).Single();
            var content=form.Controls.Find("EditorContent",true).Single();
            File.WriteAllText("light-layout-diagnostics.txt", "Window="+form.Size+" Minimum="+form.MinimumSize+" Client="+form.ClientSize+" Viewport="+viewport.ClientSize+" Display="+viewport.DisplayRectangle+" Content="+content.Bounds+" ContentMin="+content.MinimumSize+" Horizontal="+viewport.HorizontalScroll.Visible);
            Snapshot(form,"lightmove-light-small.png");
            Check(!viewport.HorizontalScroll.Visible,"Minimum desktop window needs horizontal scrolling");
        }
    }
    private static string ReferenceSignature(CurveCanvas canvas) => string.Join(";",canvas.AppliedPoints.Select(p=>p.X.ToString("R")+","+p.Y.ToString("R")));
    private static void AppliedPointTests()
    {
        var profile=Natural(); var args=profile.argsX; args.limit=2.8; profile.argsX=args; profile.argsY=args;
        Profile applied=null;
        using(var form=new CurveEditorForm(profile,p=>{applied=CurveProfile.Copy(p); return Task.FromResult(0);},statePath+".reference"))
        {
            Show(form); var canvas=Field<CurveCanvas>(form,"canvas");
            Check(canvas.AppliedPoints.Count==canvas.Model.Points.Count,"Applied reference markers are missing");
            var sampler=CurveProfile.Sampler(profile.argsX);
            Check(canvas.AppliedPoints.All(p=>Math.Abs(p.Y-sampler(p.X))<1e-10),"Reference markers do not lie on the actual applied curve");
            string original=ReferenceSignature(canvas);
            Descendants(form).OfType<Button>().Single(b=>b.Text=="均衡顺手").PerformClick();
            Check(ReferenceSignature(canvas)==original,"Choosing a preview preset changes applied markers");
            string preview=CurveSignature(canvas); var reference=canvas.AppliedPoints.Last();
            Mouse(canvas,"OnMouseDown",MouseButtons.Left,At(canvas,reference.X,reference.Y));
            Mouse(canvas,"OnMouseMove",MouseButtons.Left,At(canvas,50,2.7));
            Mouse(canvas,"OnMouseUp",MouseButtons.Left,At(canvas,50,2.7));
            Check(CurveSignature(canvas)==preview && ReferenceSignature(canvas)==original,"Read-only gray markers can be dragged");
            Snapshot(form,"lightmove-reference-comparison.png");
            Field<Button>(form,"apply").PerformClick(); Pump(()=>applied!=null && form.Enabled);
            var appliedSampler=CurveProfile.Sampler(applied.argsX);
            Check(ReferenceSignature(canvas)!=original && canvas.AppliedPoints.All(p=>Math.Abs(p.Y-appliedSampler(p.X))<1e-10),"Reference markers do not update after successful Apply");
            string active=ReferenceSignature(canvas); form.UndoCurve();
            Check(ReferenceSignature(canvas)==active,"Undoing preview changes the applied reference markers");
        }
        var separate=Natural(); var y=separate.argsY; y.limit=2.8; separate.argsY=y;
        using(var form=new CurveEditorForm(separate,p=>Task.FromResult(0),statePath+".reference-axes"))
        {
            Show(form); var canvas=Field<CurveCanvas>(form,"canvas"); var axis=Field<ComboBox>(form,"axis");
            string x=ReferenceSignature(canvas); axis.SelectedIndex=1;
            Check(ReferenceSignature(canvas)!=x && canvas.AppliedPoints.All(p=>Math.Abs(p.Y-CurveProfile.Sampler(separate.argsY)(p.X))<1e-10),"Reference markers do not follow the selected axis");
        }
    }
    private sealed class FakeStartup : IStartupRegistration
    {private string command;public int Writes;public string Command {get=>command;set {command=value;Writes++;}}}
    private sealed class FailingStore : DesktopSettingsStore
    {
        public FailingStore(string path):base(path) {}
        public override void Save(DesktopPreferences preferences) { throw new IOException("Test save failure"); }
    }
    private static void DesktopTests()
    {
        string path=statePath+".desktop.json"; if(File.Exists(path)) File.Delete(path);
        var registration=new FakeStartup();
        var service=new DesktopSettingsService(new DesktopSettingsStore(path),registration,@"C:\Light Move\LightMove.exe");
        Check(!service.AutoStart && service.Current.CloseToTray && service.Current.StartInTray,"Unexpected startup/tray defaults");
        service.RefreshStartupTarget(); Check(registration.Command==null,"Startup target refresh implicitly enables autostart");
        service.Save(true,new DesktopPreferences {StartInTray=false,CloseToTray=true});
        Check(registration.Command=="\"C:\\Light Move\\LightMove.exe\" --background","Startup path is not correctly quoted");
        var reloaded=new DesktopSettingsService(new DesktopSettingsStore(path),registration,@"C:\New LightMove\LightMove.exe");
        Check(!reloaded.Current.StartInTray && reloaded.AutoStart,"Desktop preferences are not persisted");
        reloaded.RefreshStartupTarget(); Check(registration.Command.Contains("New LightMove"),"Enabled autostart is not migrated to the current executable");
        string before=registration.Command;
        var failing=new DesktopSettingsService(new FailingStore(path),registration,@"C:\LightMove.exe");
        bool failed=false; try { failing.Save(false,new DesktopPreferences()); } catch(IOException) { failed=true; }
        Check(failed && registration.Command==before,"Failed preference save does not restore startup registration");
        service.Save(false,new DesktopPreferences()); Check(!service.AutoStart && registration.Command==null,"Disable autostart leaves a registration behind");
        using(var form=new DesktopSettingsForm(service))
        {
            Show(form); CheckTextFits(form); Snapshot(form,"lightmove-startup-settings.png");
            var auto=(CheckBox)form.Controls.Find("AutoStart",true).Single(); var hidden=form.Controls.Find("StartInTray",true).Single();
            Check(!hidden.Enabled,"Start-hidden control is enabled when autostart is off");
            auto.Checked=true; Check(hidden.Enabled,"Autostart toggle does not enable the start-hidden choice");
            ((Button)form.Controls.Find("SaveDesktopSettings",true).Single()).PerformClick();
            Check(service.AutoStart && form.DialogResult==DialogResult.OK,"Startup settings page does not save");
        }
        using(var home=new CurveEditorForm(Natural(),p=>Task.FromResult(0),statePath+".tray"))
        using(var session=new TraySession(home,service,false,startCursorLocator:false))
        {
            home.Opacity=0; Application.DoEvents(); var canvas=Field<CurveCanvas>(home,"canvas");
            canvas.Selected=2; canvas.ChangeSelected(12,1.4); string preview=CurveSignature(canvas);
            var pointerSwitch=(CheckBox)home.Controls.Find("ShakeCursorSwitch",true).Single();
            var pointerHelp=home.Controls.Find("ShakeCursorDescription",true).Single();
            Check(pointerSwitch.Checked && pointerHelp.Text.Contains("摇晃") && pointerHelp.Text.Contains("拖拽"),"Home pointer switch or instructions are missing");
            int startupWrites=registration.Writes;
            pointerSwitch.GetType().GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pointerSwitch,new object[]{EventArgs.Empty});
            Check(!pointerSwitch.Checked && !service.Current.ShakeToFindCursor && CurveSignature(canvas)==preview,"Home switch does not persist independently of the curve");
            Check(registration.Writes==startupWrites,"Home pointer switch changes startup registration");
            var pointerMenu=Field<ToolStripMenuItem>(session,"shakeMenu");
            Check(!pointerMenu.Checked,"Home switch does not sync tray state");
            pointerMenu.PerformClick();
            Check(pointerSwitch.Checked && service.Current.ShakeToFindCursor,"Tray switch does not sync home state");
            service.SetShakeToFindCursor(false);
            Check(!pointerSwitch.Checked && !pointerMenu.Checked,"Preference changes do not sync pointer controls");
            service.SetShakeToFindCursor(true);CheckTextFits(home);Snapshot(home,"lightmove-pointer-switch.png");
            ((Button)home.Controls.Find("CloseWindow",true).Single()).PerformClick(); Application.DoEvents();
            Check(session.IsHidden && !home.IsDisposed,"Closing home exits instead of hiding to tray");
            session.Restore(); Application.DoEvents();
            Check(home.Visible && CurveSignature(canvas)==preview,"Restoring from tray loses the unapplied curve");
            home.WindowState=FormWindowState.Minimized; Application.DoEvents(); Check(session.IsHidden,"Minimize does not hide to tray");
            session.Restore(); Check(home.WindowState==FormWindowState.Normal && home.Visible,"Tray restore remains minimized");
            home.Enabled=false; session.RequestExit(); Check(!home.IsDisposed,"Tray exit interrupts an active apply");
            home.Enabled=true; session.RequestExit(); Check(home.IsDisposed,"Explicit tray exit fails to close the app");
        }
        using(var signal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset))
        using(var home=new CurveEditorForm(Natural(),p=>Task.FromResult(0),statePath+".background"))
        using(var session=new TraySession(home,service,true,signal,startCursorLocator:false))
        {
            home.Opacity=0; Check(session.IsHidden && home.IsHandleCreated,"Background launch flashes or omits the home handle");
            signal.Set(); Pump(()=>home.Visible); Check(home.WindowState==FormWindowState.Normal,"Second launch signal does not restore the existing window");
            service.Save(false,new DesktopPreferences {CloseToTray=false}); home.Close(); Check(home.IsDisposed,"Disabling close-to-tray does not restore normal closing");
        }
        using(var home=new CurveEditorForm(Natural(),p=>Task.FromResult(0),statePath+".pointer-failure"))
        using(var session=new TraySession(home,new DesktopSettingsService(new FailingStore(path),registration,"LightMove.exe"),false,startCursorLocator:false))
        {
            home.Opacity=0;var pointerSwitch=(CheckBox)home.Controls.Find("ShakeCursorSwitch",true).Single();bool beforeChecked=pointerSwitch.Checked;
            pointerSwitch.GetType().GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pointerSwitch,new object[]{EventArgs.Empty});
            Check(pointerSwitch.Checked==beforeChecked && home.Controls.Find("ShakeCursorDescription",true).Single().Text.Contains("保存未完成"),"Failed home toggle falsely changes state or hides its error");
            session.RequestExit();
        }
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
        var expectedLut = DriverConfig.FromProfile(nativeProfile);
        var readbackLut = DriverConfig.Convert(expectedLut.ToJSON()).Item1;
        Check(!Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.Parse(expectedLut.ToJSON()), readbackLut.ToJObject()),
            "Precision regression fixture no longer reproduces Single/Double mismatch");
        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(expectedLut.ToJObject(), readbackLut.ToJObject()),
            "Identical LUT values fail typed readback comparison");
        var changedLut = CurveProfile.Copy(readbackLut.profiles[0]);
        var changedArgs = changedLut.argsX;
        changedArgs.data[3] += .000001f; changedLut.argsX = changedArgs;
        var changedConfig = DriverConfig.FromProfile(changedLut);
        Check(!Newtonsoft.Json.Linq.JToken.DeepEquals(expectedLut.ToJObject(), changedConfig.ToJObject()),
            "Readback comparison silently permits changed float data");
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
        int calls = 0;
        using (var canvas = new CurveCanvas { Size = new Size(600, 400), Model = many.Copy(), Baseline = x => { calls++; return 1; } })
        using (var bitmap = new Bitmap(600, 400))
        {
            canvas.DrawToBitmap(bitmap, new Rectangle(0,0,600,400)); int before = calls;
            canvas.UpdateLivePosition(20, true); canvas.DrawToBitmap(bitmap, new Rectangle(0,0,600,400));
            Check(calls - before < 5, "Live marker redraw recalculates the entire native curve");
            before = calls; canvas.Selected = 2; canvas.ChangeSelected(11, 1.8);
            canvas.DrawToBitmap(bitmap, new Rectangle(0,0,600,400));
            Check(calls - before >= 361, "Dragging failed to invalidate curve cache");
        }
    }
    private static void ComparisonTests()
    {
        string path = statePath + ".compare";
        if (File.Exists(path + ".comparison")) File.Delete(path + ".comparison");
        var tuned = Natural(); tuned.rotation = 9; tuned.yxOutputDPIRatio = 1.2;
        var y = tuned.argsY; y.limit = 2.4; tuned.argsY = y;
        string signature = CurveProfile.Signature(tuned);
        Profile active = CurveProfile.Copy(tuned); int calls = 0;
        var gate = new TaskCompletionSource<bool>(); bool delay = false, fail = false;
        Func<Profile,Task> callback = async p => { calls++; if (delay) await gate.Task; if (fail) throw new IOException("测试：驱动未确认"); active = CurveProfile.Copy(p); };
        using (var form = new CurveEditorForm(tuned,callback,path))
        {
            Show(form);
            var control = (FeelSwitch)form.Controls.Find("FeelComparisonSwitch",true).Single();
            var canvas = Field<CurveCanvas>(form,"canvas");
            Check(control.Checked && control.Width >= 280 && control.Height >= 64,"Comparison switch is not prominent / initially on");
            Check(control.Parent.Parent.Name=="LiveFeelCard","Comparison switch is not grouped with live feedback");
            Drag(canvas,2,12,1.5);
            string draft = CurveProfile.Signature(form.PreviewProfile());
            // Keep a pending preview while exercising the real asynchronous UI path.
            delay = true; var off = form.ToggleComparisonAsync();
            Check(!form.Enabled && control.Busy,"Switch does not show its busy state");
            var duplicate = form.ToggleComparisonAsync();
            Check(calls == 1 && duplicate.IsCompleted,"Fast repeated toggle submitted a second driver write");
            gate.SetResult(true); Pump(()=>off.IsCompleted && form.Enabled); delay = false;
            Check(!control.Checked && !control.Busy,"Switch did not enter original-speed state");
            var neutral = new ManagedAccel(active);
            foreach(double speed in new[] { 1.0, 10, 60, 500 })
            {
                var result = neutral.Accelerate((int)(speed*1000),(int)(speed*1000),1,1000);
                Check(Math.Abs(result.Item1-speed*1000)<.001 && Math.Abs(result.Item2-speed*1000)<.001,"Neutral profile still accelerates / rotates / scales");
            }
            Check(CurveProfile.Signature(form.PreviewProfile()) == draft && Field<Button>(form,"apply").Enabled,"Comparison changed pending preview");
            Check(File.Exists(path+".comparison"),"Off state has no durable return path");
            var now=DateTime.UtcNow; form.UpdateLiveSample(new LiveMouseSample { TimestampUtc=now,SpeedX=20,SpeedY=40 },now);
            Check(canvas.LiveMoving && Math.Abs(canvas.LiveAppliedMultiplier-1)<.00001,"Red marker is not on neutral 1x curve");
            Check(canvas.AppliedPoints.All(p=>Math.Abs(p.Y-1)<.00001),"Reference diamonds do not show neutral curve");
            Snapshot(form,"comparison-off-preview.png");
            // Reopen while paused: retain exact asymmetric profile AND saved control points.
            using(var reopened = new CurveEditorForm(active,callback,path))
            {
                Show(reopened); Check(!((FeelSwitch)reopened.Controls.Find("FeelComparisonSwitch",true).Single()).Checked,"Restart forgot paused state");
                Check(CurveProfile.Signature(Field<Profile>(reopened,"source"))==signature,"Restart lost the tuned asymmetric profile");
            }
            fail = true; var failed = form.ToggleComparisonAsync(); Pump(()=>failed.IsCompleted && form.Enabled);
            Check(!control.Checked && control.Error != null && File.Exists(path+".comparison"),"Failed resume discarded recovery / claims enabled");
            Snapshot(form,"comparison-error.png");
            fail = false; var on = form.ToggleComparisonAsync(); Pump(()=>on.IsCompleted && form.Enabled);
            Check(control.Checked && control.Error == null && CurveProfile.Signature(active)==signature,"Resume did not restore exact full profile");
            Check(CurveProfile.Signature(form.PreviewProfile())==draft && !File.Exists(path+".comparison"),"Resume changed draft / left journal");
            form.UndoCurve(); Check(!Field<Button>(form,"apply").Enabled,"Comparison broke undo history");
            form.RedoCurve(); Check(CurveProfile.Signature(form.PreviewProfile())==draft,"Comparison broke redo history");
            var pause = form.ToggleComparisonAsync(); Pump(()=>pause.IsCompleted && form.Enabled);
            Field<Button>(form,"apply").PerformClick(); Pump(()=>form.Enabled && !Field<Button>(form,"apply").Enabled);
            Check(control.Checked && CurveProfile.Signature(active)==draft,"Apply while paused did not enable the new curve");
            Snapshot(form,"comparison-on.png");
            var pausedAgain=form.ToggleComparisonAsync(); Pump(()=>pausedAgain.IsCompleted && form.Enabled);
            using(var reopened=new CurveEditorForm(active,callback,path))
            {
                Show(reopened);
                Check(!((FeelSwitch)reopened.Controls.Find("FeelComparisonSwitch",true).Single()).Checked,"Saved LUT restart forgot comparison state");
                Check(Math.Abs(Field<CurveCanvas>(reopened,"canvas").Model.Points[2].X-12)<.2,"Paused restart lost saved control points");
                var resume=reopened.ToggleComparisonAsync(); Pump(()=>resume.IsCompleted && reopened.Enabled);
                Check(CurveProfile.Signature(active)==draft,"Paused restart could not restore saved LUT exactly");
            }
        }
        string recovery = path+".recovery"; if(File.Exists(recovery)) File.Delete(recovery);
        var comparison = new FeelComparison(tuned,recovery);
        bool rejected=false;
        try { comparison.ToggleAsync(p=>{throw new IOException("reject");}).GetAwaiter().GetResult(); } catch(IOException) { rejected=true; }
        Check(rejected && !comparison.Paused,"Failed pause reports success");
        Check(!new FeelComparison(tuned,recovery).Paused,"Failed pause is restored as paused when driver remains enabled");
        File.WriteAllText(recovery,"broken json"); var broken=new FeelComparison(tuned,recovery);
        Check(broken.RecoveryError!=null && CurveProfile.Signature(broken.EnabledProfile)==signature,"Corrupt backup silently replaces active curve");
        broken.Applied(tuned); Check(!File.Exists(recovery),"Applying valid curve does not repair corrupt comparison state");
        string blocked = Path.Combine(path+".missing-parent","journal");
        bool submitted = false; rejected=false;
        try { new FeelComparison(tuned,blocked).ToggleAsync(p=>{submitted=true;return Task.FromResult(0);}).GetAwaiter().GetResult(); } catch(IOException) { rejected=true; }
        Check(rejected && !submitted,"Driver paused before its return path was saved");
    }
    private static void UserPresetTests()
    {
        string path=statePath+".user-presets";
        if(File.Exists(path))File.Delete(path);
        var store=new UserPresetStore(path);
        Check(store.Items.Count==0,"Missing user presets is not an empty collection");
        int calls=0;
        using(var form=new CurveEditorForm(Natural(),p=>{calls++;return Task.FromResult(0);},statePath+".user-preset-editor"))
        {
            Show(form);var canvas=Field<CurveCanvas>(form,"canvas");
            Check(canvas.Plot.Height/canvas.Plot.Width>=.20 && canvas.Plot.Height/canvas.Plot.Width<=.55,"Default curve editor lost the first release's moderate proportions");
            var viewport=(Panel)form.Controls.Find("ContentViewport",true).Single();
            Check(viewport.VerticalScroll.Visible,"Tall curve editor does not scroll");
            Drag(canvas,2,12,1.6);var preset=form.CaptureUserPreset();preset.Name="办公精调";
            var saved=store.Add(preset);preset.X[2].Y=9;
            Check(Math.Abs(store.Items[0].X[2].Y-1.6)<.01,"Preset storage shares mutable editor points");
            var second=store.Add(saved);
            Check(second.Name!=saved.Name && store.Items.Count==2,"Same-name save overwrites a preset");
            store=new UserPresetStore(path);
            Check(store.Items.Count==2 && store.Items.Any(p=>p.Id==saved.Id),"Saved presets do not survive reopening");
            var removed=store.Delete(saved.Id);store.Restore(removed);
            Check(new UserPresetStore(path).Items.Any(p=>p.Id==saved.Id),"Undo preset deletion did not persist");
            form.UndoCurve();Check(!Field<Button>(form,"apply").Enabled,"Undo before preset load failed");
            form.LoadUserPreset(saved);
            Check(calls==0 && Field<Button>(form,"apply").Enabled && Math.Abs(canvas.Model.Points[2].Y-1.6)<.01,"Loading preset applies driver / loses curve");
            form.UndoCurve();Check(!Field<Button>(form,"apply").Enabled,"Loading a preset is not undoable");
            form.RedoCurve();Check(Math.Abs(canvas.Model.Points[2].Y-1.6)<.01,"Redo preset load lost curve");
            var asymmetric=saved.Copy();asymmetric.Linked=false;asymmetric.Y[2].Y=2.3;
            form.LoadUserPreset(asymmetric);var preview=form.PreviewProfile();
            Check(!Field<CheckBox>(form,"sync").Checked && Math.Abs(CurveProfile.Sampler(preview.argsY)(12)-2.3)<.001,"Preset load lost separate Y curve");
            string before=File.ReadAllText(path);var invalid=saved.Copy();invalid.Name=" ";bool rejected=false;
            try{store.Add(invalid);}catch(ArgumentException){rejected=true;}
            Check(rejected && File.ReadAllText(path)==before,"Invalid preset save changed the file");
            using(var manager=new UserPresetsForm(store,form.CaptureUserPreset))
            {
                Show(manager);CheckTextFits(manager);
                var name=(TextBox)manager.Controls.Find("PresetName",true).Single();name.Text="游戏手感";
                ((Button)manager.Controls.Find("SaveUserPreset",true).Single()).PerformClick();
                Check(store.Items.Count==3 && store.Items.Any(p=>p.Name=="游戏手感"),"Preset manager cannot save current preview");
                var delete=Descendants(manager).OfType<Button>().Single(b=>b.Text=="删除");delete.PerformClick();
                Check(store.Items.Count==2,"Preset manager cannot delete");
                Descendants(manager).OfType<Button>().Single(b=>b.Text=="撤回删除").PerformClick();
                Check(store.Items.Count==3,"Preset manager cannot undo deletion");
                Snapshot(manager,"lightmove-user-presets.png");
                ((Button)manager.Controls.Find("LoadUserPreset",true).Single()).PerformClick();
                Check(manager.SelectedPreset!=null && calls==0,"Preset manager failed to return a preview without writing driver");
            }
            var unavailable=new UserPresetStore(Path.Combine(path+".missing-directory","presets.json"));
            bool failed=false;try{unavailable.Add(saved);}catch(IOException){failed=true;}
            Check(failed && unavailable.Items.Count==0,"Failed save changed in-memory preset list");
        }
        File.WriteAllText(path,"broken-json");bool corrupt=false;
        try{new UserPresetStore(path);}catch(Newtonsoft.Json.JsonException){corrupt=true;}
        Check(corrupt && File.ReadAllText(path)=="broken-json","Corrupt preset file was overwritten");
        File.Delete(path);
    }
    private static void EditorTests()
    {
        int applied = 0; Profile last = null;
        Func<Profile, Task> callback = async profile => { await Task.Delay(30); applied++; last = CurveProfile.Copy(profile); };
        using (var form = new CurveEditorForm(Natural(), callback, statePath))
        {
            Show(form);
            CheckTextFits(form);
            Check(!Descendants(form).OfType<Button>().Any(b => b.Text.Contains("原版高级")), "Legacy advanced entry remains on the home page");
            var canvas = Field<CurveCanvas>(form, "canvas"); var apply = Field<Button>(form, "apply");
            Check(applied == 0 && !apply.Enabled && !File.Exists(statePath), "Opening editor changes settings");
            var snap = Field<CheckBox>(form, "snapGrid");
            var precision = Field<ComboBox>(form, "gridPrecision");
            Check(snap.Checked && canvas.SnapToGrid && canvas.GridSpeedStep == 1 && canvas.GridMultiplierStep == .1, "Standard snapping is not the default");
            snap.Checked = false; precision.SelectedIndex = 1;
            Check(!canvas.SnapToGrid && canvas.GridSpeedStep == .5 && canvas.GridMultiplierStep == .05, "Grid controls do not update the canvas");
            Check(applied == 0 && !apply.Enabled, "Changing grid preferences applies or dirties a curve");
            snap.Checked = true; precision.SelectedIndex = 0;
            Snapshot(form, "curve-editor.png");
            var now = DateTime.UtcNow;
            form.UpdateLiveSample(new LiveMouseSample { TimestampUtc = now, SpeedX = 20, SpeedY = 40 }, now);
            Check(canvas.LiveMoving && canvas.LiveSpeed == 20, "Fresh live sample did not move red marker");
            double liveApplied = canvas.LiveAppliedMultiplier;
            Check(Math.Abs(liveApplied - CurveProfile.Sampler(Natural().argsX)(20)) < .00001, "Live marker is not on the applied curve");
            double axisX = canvas.MaxX, axisY = canvas.MaxY;
            Drag(canvas, 3, 23, 1.65);
            Check(canvas.Selected == 3 && Math.Abs(canvas.Model.Points[3].X - 23) < .2, "Mouse drag did not move control point");
            Check(canvas.MaxX == axisX && canvas.MaxY == axisY, "Dragging unexpectedly rescaled axes");
            Check(applied == 0 && apply.Enabled, "Preview writes to driver before Apply");
            Check(Math.Abs(canvas.LiveAppliedMultiplier - liveApplied) < .00001 && Math.Abs(canvas.LivePreviewMultiplier - liveApplied) > .02, "Dragging preview moved the applied red marker");
            Snapshot(form, "lightmove-live-preview.png");
            form.UpdateLiveSample(new LiveMouseSample { TimestampUtc = now.AddSeconds(-1), SpeedX = 99 }, now);
            Check(!canvas.LiveMoving && canvas.LiveSpeed == 0, "Stale sample did not return to idle");
            form.UpdateLiveSample(new LiveMouseSample { TimestampUtc = now, SpeedX = double.NaN }, now);
            Check(!canvas.LiveMoving && canvas.LiveSpeed == 0, "Invalid sample breaks live marker");
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
            Field<ComboBox>(form, "axis").SelectedIndex = 1;
            var now = DateTime.UtcNow;
            form.UpdateLiveSample(new LiveMouseSample { TimestampUtc = now, SpeedX = 5, SpeedY = 40 }, now);
            Check(canvas.LiveSpeed == 40, "Live marker ignored selected Y axis");
            Field<ComboBox>(form, "axis").SelectedIndex = 0;
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
            var viewport = (Panel)form.Controls.Find("ContentViewport", true).Single();
            var actions = form.Controls.Find("EditorActions", true).Single();
            Check(viewport.AutoScroll && viewport.VerticalScroll.Visible, "Small window cannot scroll through content");
            Check(actions.Bottom <= form.ClientSize.Height && actions.Top >= 0, "Apply bar leaves the visible window");
            viewport.AutoScrollPosition = new Point(0, 10000); Application.DoEvents();
            Snapshot(form, "curve-editor-small-scrolled.png");
        }
        using (var setup = new FirstRunForm("测试首次使用页面（未调用安装）"))
        {
            Show(setup); Snapshot(setup, "lightmove-first-run.png");
            CheckTextFits(setup);
            Check(!setup.ReadyToOpen, "Onboarding incorrectly assumes a ready driver");
        }
        using (var form = new CurveEditorForm(Natural(), callback, statePath + ".alignment"))
        {
            Show(form); var canvas = Field<CurveCanvas>(form, "canvas");
            var selected = canvas.Model.Points[3];
            Mouse(canvas, "OnMouseDown", MouseButtons.Left, At(canvas, selected.X, selected.Y));
            canvas.DragSelectedTo(24.2, canvas.Model.Points[4].Y + .0001, Keys.Shift);
            Check(canvas.AlignmentGuideVisible && canvas.Model.Points[3].X == 24, "Editor does not show Shift alignment guide");
            Snapshot(form, "lightmove-alignment-preview.png");
            Mouse(canvas, "OnMouseUp", MouseButtons.Left, At(canvas,24,canvas.Model.Points[3].Y));
            var more = Descendants(form).OfType<Button>().Single(b => b.Text.StartsWith("更多设置"));
            more.PerformClick(); Application.DoEvents(); CheckTextFits(form);
            Snapshot(form, "lightmove-more-settings.png");
            var coordinate = Field<NumericUpDown>(form, "speed");
            coordinate.Value = 0; coordinate.Value = 1;
            Check((double)coordinate.Value == canvas.Model.Points[canvas.Selected].X, "Unchanged clamped numeric input displays a coordinate different from the curve");
        }
    }
    private static void SnapTests()
    {
        var model = new CurveModel(new[] { new CurvePoint(0,1), new CurvePoint(3,1.12345), new CurvePoint(10,1.234567), new CurvePoint(25,1.55), new CurvePoint(60,1.8) });
        using (var canvas = new CurveCanvas { Size = new Size(1200, 600), Model = model, MaxX = 60, MaxY = 3, Selected = 2 })
        {
            canvas.CreateControl();
            int changed = 0; canvas.CurveChanged += (s,e) => changed++;
            canvas.DragSelectedTo(12.37,1.368,Keys.None);
            Check(model.Points[2].X == 12 && model.Points[2].Y == 1.4, "Standard grid does not snap speed/multiplier");
            int sameCell = changed; canvas.DragSelectedTo(12.2,1.39,Keys.None);
            Check(changed == sameCell, "Movement within one snapped cell dirties/recalculates an unchanged curve");
            canvas.DragSelectedTo(12.37,1.368,Keys.Alt);
            Check(model.Points[2].X == 12.37 && model.Points[2].Y == 1.368, "Alt does not temporarily bypass grid snapping");
            canvas.GridSpeedStep = .5; canvas.GridMultiplierStep = .05;
            canvas.DragSelectedTo(12.37,1.368,Keys.None);
            Check(model.Points[2].X == 12.5 && model.Points[2].Y == 1.35, "Fine grid step differs from displayed settings");
            canvas.DragSelectedTo(12.37,1.12,Keys.Shift);
            Check(model.Points[2].Y == model.Points[1].Y && canvas.AlignmentGuideVisible && canvas.AlignmentGuideY == model.Points[1].Y, "Shift fails to align an off-grid neighboring height exactly");
            canvas.DragSelectedTo(12.37,1.12,Keys.Shift | Keys.Alt);
            Check(model.Points[2].Y == 1.12 && !canvas.AlignmentGuideVisible, "Alt must take precedence over Shift alignment");
            canvas.SnapToGrid = false;
            canvas.DragSelectedTo(12.37,2.328,Keys.None);
            Check(model.Points[2].X == 12.37 && model.Points[2].Y == 2.328, "Disabling grid does not restore free dragging");
            canvas.DragSelectedTo(12.37,2.328,Keys.Shift);
            Check(model.Points[2].X == 12.5 && model.Points[2].Y == 2.35 && !canvas.AlignmentGuideVisible, "Shift should enable grid without pulling distant points to a guide");
            canvas.SnapToGrid = true;
            canvas.DragSelectedTo(-100,99,Keys.None);
            Check(model.Points[2].X == 3.5 && model.Points[2].Y == 3, "Snapping crosses neighbors or graph bounds");
            canvas.DragSelectedTo(100,-99,Keys.None);
            Check(model.Points[2].X == 24.5 && model.Points[2].Y == .1, "Snapped upper neighbor/lower multiplier bounds");
            canvas.Selected = 0; canvas.DragSelectedTo(15,1.13,Keys.None);
            Check(model.Points[0].X == 0, "Snapping moved the fixed first speed");
            canvas.Selected = 2; canvas.ChangeSelected(12.37,1.368);
            Check(model.Points[2].X == 12.37 && model.Points[2].Y == 1.368, "Grid silently rounds numeric entry");
            typeof(CurveCanvas).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new KeyEventArgs(Keys.Up) });
            Check(Math.Abs(model.Points[2].Y - 1.418) < 1e-10, "Arrow key does not nudge by the chosen step");
            canvas.DragSelectedTo(12.37,1.12,Keys.Shift);
            Mouse(canvas,"OnMouseUp",MouseButtons.Left,At(canvas,12.37,1.12));
            Check(!canvas.AlignmentGuideVisible, "Alignment guide stays stuck after release");
            int before = changed; canvas.DragSelectedTo(double.NaN,1,Keys.None);
            Check(changed == before, "Invalid pointer coordinate changes the curve");
            Check(CurveCanvas.SnapCoordinate(.3,1,.26,.49) == .3, "Narrow interval with no grid point crosses a neighbor");
        }
    }
    private static string CurveSignature(CurveCanvas canvas) => Newtonsoft.Json.JsonConvert.SerializeObject(canvas.Model.Points);
    private static bool Shortcut(Form form, Keys keys)
    {
        var arguments = new object[] { Message.Create(form.Handle,0x100,IntPtr.Zero,IntPtr.Zero), keys };
        return (bool)form.GetType().GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,arguments);
    }
    private static void HistoryTests()
    {
        int applied = 0;
        Func<Profile,Task> callback = p => { applied++; return Task.FromResult(0); };
        using (var form = new CurveEditorForm(Natural(), callback, statePath + ".undo"))
        {
            Show(form); var canvas = Field<CurveCanvas>(form,"canvas");
            var undo = Field<Button>(form,"undo"); var redo = Field<Button>(form,"redo"); var apply = Field<Button>(form,"apply");
            string original = CurveSignature(canvas);
            Check(!undo.Enabled && !redo.Enabled, "New editor has history");
            Mouse(canvas,"OnMouseDown",MouseButtons.Left,At(canvas,canvas.Model.Points[2].X,canvas.Model.Points[2].Y));
            Mouse(canvas,"OnMouseMove",MouseButtons.Left,At(canvas,14,1.4));
            Mouse(canvas,"OnMouseMove",MouseButtons.Left,At(canvas,16,1.5));
            Mouse(canvas,"OnMouseUp",MouseButtons.Left,At(canvas,16,1.5));
            string first = CurveSignature(canvas);
            Check(undo.Enabled && !redo.Enabled, "Completed drag was not recorded");
            undo.PerformClick();
            Check(CurveSignature(canvas) == original && !undo.Enabled && !apply.Enabled, "A multi-move drag is not undone as one complete step");
            redo.PerformClick();
            Check(CurveSignature(canvas) == first && apply.Enabled && applied == 0, "Redo failed or wrote to driver");
            Mouse(canvas,"OnMouseDown",MouseButtons.Left,At(canvas,canvas.Model.Points[2].X,canvas.Model.Points[2].Y));
            Mouse(canvas,"OnMouseUp",MouseButtons.Left,At(canvas,canvas.Model.Points[2].X,canvas.Model.Points[2].Y));
            form.UndoCurve(); Check(CurveSignature(canvas) == original && !undo.Enabled, "Selecting without moving creates a spurious undo step");
            form.RedoCurve(); Drag(canvas,2,18,1.6); form.UndoCurve();
            Check(CurveSignature(canvas) == first && redo.Enabled, "Second edit cannot be undone independently");
            Drag(canvas,2,15,1.3); Check(!redo.Enabled, "A new edit after Undo preserves an obsolete redo branch");
            form.UndoCurve(); form.UndoCurve(); Check(CurveSignature(canvas) == original, "Undo branch lost the original curve");
            var multiplier = Field<NumericUpDown>(form,"multiplier"); var speed = Field<NumericUpDown>(form,"speed");
            multiplier.Value = 1.4m; speed.Value = 12;
            form.UndoCurve(); Check(canvas.Model.Points[2].X == 10 && canvas.Model.Points[2].Y == 1.4, "Numeric speed undo also loses earlier multiplier edit");
            form.UndoCurve(); Check(CurveSignature(canvas) == original, "Numeric edits do not undo independently");
            int count = canvas.Model.Points.Count;
            Mouse(canvas,"OnMouseDoubleClick",MouseButtons.Left,At(canvas,35,1.7),2);
            string added = CurveSignature(canvas);
            form.UndoCurve(); Check(canvas.Model.Points.Count == count, "Add point cannot be undone");
            form.RedoCurve(); Check(CurveSignature(canvas) == added, "Add point cannot be redone");
            Field<Button>(form,"deletePoint").PerformClick();
            form.UndoCurve(); Check(CurveSignature(canvas) == added && canvas.Selected < canvas.Model.Points.Count, "Delete point undo lost coordinates or selection");
            form.UndoCurve(); Check(CurveSignature(canvas) == original, "Undo after delete/add does not return to original");
            var preset = Descendants(form).OfType<Button>().Single(b => b.Text == "快速跨屏"); preset.PerformClick();
            form.UndoCurve(); Check(CurveSignature(canvas) == original, "Preset cannot be undone");
            Drag(canvas,2,14,1.4);
            var clear = Descendants(form).OfType<Button>().Single(b => b.Name == "ClearPreview"); clear.PerformClick();
            Check(CurveSignature(canvas) == original && !apply.Enabled, "Clear preview does not reset to applied curve");
            form.UndoCurve(); Check(canvas.Model.Points[2].X == 14 && apply.Enabled, "Clearing preview cannot be undone");
            string beforeApply = CurveSignature(canvas); apply.PerformClick(); Pump(() => applied == 1 && form.Enabled);
            form.UpdateLiveSample(new LiveMouseSample { TimestampUtc=DateTime.UtcNow, SpeedX=20, SpeedY=20 },DateTime.UtcNow);
            double baseline = canvas.LiveAppliedMultiplier;
            form.UndoCurve();
            Check(applied == 1 && apply.Enabled && Math.Abs(canvas.LiveAppliedMultiplier-baseline)<1e-10, "Undo after Apply changes live curve or writes to driver");
            form.RedoCurve(); Check(CurveSignature(canvas) == beforeApply && !apply.Enabled && applied == 1, "Redo to applied state is still marked dirty");
            Snapshot(form,"lightmove-undo-preview.png");
        }
        using (var form = new CurveEditorForm(Natural(), callback, statePath + ".undo-axes"))
        {
            Show(form); var canvas=Field<CurveCanvas>(form,"canvas"); var sync=Field<CheckBox>(form,"sync"); var axis=Field<ComboBox>(form,"axis");
            sync.Checked=false; axis.SelectedIndex=1; string originalY=CurveSignature(canvas);
            Drag(canvas,2,14,1.5); form.UndoCurve();
            Check(axis.SelectedIndex==1 && CurveSignature(canvas)==originalY && !sync.Checked, "Undo loses the edited axis or modifies another axis");
            form.UndoCurve(); Check(sync.Checked, "Axis linking change cannot be undone");
            form.RedoCurve(); form.RedoCurve(); Check(!sync.Checked && axis.SelectedIndex==1 && canvas.Model.Points[2].X==14, "Redo does not restore axis and curve together");
        }
        using (var form = new CurveEditorForm(Natural(),callback,statePath+".undo-limit"))
        {
            Show(form); var canvas=Field<CurveCanvas>(form,"canvas"); canvas.Selected=2;
            for(int i=0;i<125;i++) canvas.ChangeSelected(10,.8+i*.001);
            for(int i=0;i<100;i++) form.UndoCurve();
            Check(!Field<Button>(form,"undo").Enabled && Math.Abs(canvas.Model.Points[2].Y-.824)<1e-10, "History limit fails to retain the newest 100 actions");
            int before=applied; form.UndoCurve(); Check(applied==before,"Empty Undo writes to driver");
            Field<NumericUpDown>(form,"multiplier").Focus();
            Check(Shortcut(form,Keys.Control|Keys.Y) && Math.Abs(canvas.Model.Points[2].Y-.825)<1e-10,"Ctrl+Y does not redo from a numeric field");
            Check(Shortcut(form,Keys.Control|Keys.Z) && Math.Abs(canvas.Model.Points[2].Y-.824)<1e-10,"Ctrl+Z does not undo from a numeric field");
            Check(Shortcut(form,Keys.Control|Keys.Shift|Keys.Z) && Math.Abs(canvas.Model.Points[2].Y-.825)<1e-10,"Ctrl+Shift+Z does not redo");
        }
        using (var form = new CurveEditorForm(Natural(),callback,statePath+".undo-capture"))
        {
            Show(form); var canvas=Field<CurveCanvas>(form,"canvas"); string original=CurveSignature(canvas);
            Mouse(canvas,"OnMouseDown",MouseButtons.Left,At(canvas,canvas.Model.Points[2].X,canvas.Model.Points[2].Y));
            Mouse(canvas,"OnMouseMove",MouseButtons.Left,At(canvas,14,1.4));
            canvas.Capture=false; Application.DoEvents(); form.UndoCurve();
            Check(CurveSignature(canvas)==original,"Lost pointer capture drops the drag history");
            Mouse(canvas,"OnMouseDown",MouseButtons.Left,At(canvas,canvas.Model.Points[2].X,canvas.Model.Points[2].Y));
            Mouse(canvas,"OnMouseMove",MouseButtons.Left,At(canvas,16,1.5));
            Check(Shortcut(form,Keys.Control|Keys.Z) && !canvas.Capture && CurveSignature(canvas)==original,"Undo during a captured drag fails to complete and revert one action");
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (Array.IndexOf(args,"--live-comparison-roundtrip")>=0)
            {
                var before=DriverConfig.GetActive(); string beforeJson=before.ToJSON();
                File.WriteAllText("live-comparison-before.json",beforeJson);
                File.WriteAllText("settings.json",beforeJson);
                try
                {
                    using(var backend=new RawAcceleration())
                    {
                        Thread.Sleep(1250); Application.DoEvents();
                        string journal=Path.Combine(Environment.CurrentDirectory,"live-comparison-recovery.json");
                        var comparison=new FeelComparison(before.profiles[0],journal);
                        var off=comparison.ToggleAsync(backend.AccelGUI.Settings.ApplyGraphicalProfileAsync);
                        Pump(()=>off.IsCompleted); off.GetAwaiter().GetResult();
                        var expected=DriverConfig.Convert(beforeJson).Item1;
                        expected.SetProfileAt(0,FeelComparison.Neutral(before.profiles[0]));
                        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(expected.ToJObject(),DriverConfig.GetActive().ToJObject()),"Live comparison did not apply neutral profile / changed other config");
                        var recovered=new FeelComparison(DriverConfig.GetActive().profiles[0],journal);
                        Check(recovered.Paused,"Live paused profile did not recover after restart");
                        var on=recovered.ToggleAsync(backend.AccelGUI.Settings.ApplyGraphicalProfileAsync);
                        Pump(()=>on.IsCompleted); on.GetAwaiter().GetResult();
                        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(before.ToJObject(),DriverConfig.GetActive().ToJObject()),"Live comparison did not restore exact full config");
                        var saved=DriverConfig.Convert(File.ReadAllText("settings.json")).Item1;
                        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(before.ToJObject(),saved.ToJObject()),"Live comparison did not save restored configuration");
                    }
                    File.WriteAllText("live-comparison-result.txt","PASS: neutral driver write/readback, recovery after restart, exact full config restore and settings save. No changes to device config.");
                    return 0;
                }
                finally
                {
                    Thread.Sleep(1200); before.Activate(); Thread.Sleep(1200);
                    Check(Newtonsoft.Json.Linq.JToken.DeepEquals(before.ToJObject(),DriverConfig.GetActive().ToJObject()),"Live comparison rollback failed");
                    File.WriteAllText("settings.json",beforeJson);
                }
            }
            if (Array.IndexOf(args, "--render-demo") >= 0 || Array.IndexOf(args,"--render-full-layout")>=0)
            {
                Directory.CreateDirectory("demo-frames");
                LiveMouseSample sample = null;
                using (var form = new CurveEditorForm(Natural(), p => Task.FromResult(0), "demo-state-unused.json", () => sample))
                {
                    Show(form); form.Text = "轻移 LightMove · 交互演示（模拟鼠标输入）";
                    form.GetType().GetMethod("SetPreset", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { 1.8 });
                    if(Array.IndexOf(args,"--render-full-layout")>=0 || Array.IndexOf(args,"--full-layout")>=0)
                    {
                        var content=form.Controls.Find("EditorContent",true).Single();
                        var footer=form.Controls.Find("EditorActions",true).Single();
                        var chrome=form.Controls.Find("WindowChrome",true).Single();
                        for(int pass=0;pass<3;pass++)
                        {form.ClientSize=new Size(form.ClientSize.Width,content.MinimumSize.Height+footer.Height+chrome.Height);Application.DoEvents();}
                        var now=DateTime.UtcNow;form.UpdateLiveSample(new LiveMouseSample {TimestampUtc=now,SpeedX=20,SpeedY=20},now);
                        Snapshot(form,"lightmove-publish-workspace.png");
                        var canvas=Field<CurveCanvas>(form,"canvas");
                        using(var bitmap=new Bitmap(canvas.Width,canvas.Height))
                        {canvas.DrawToBitmap(bitmap,new Rectangle(Point.Empty,canvas.Size));bitmap.Save("lightmove-publish-curve.png");}
                        if(Array.IndexOf(args,"--render-demo")<0)return 0;
                    }
                    for (int i = 0; i < 32; i++)
                    {
                        double speed = i < 16 ? i * 4 : (31 - i) * 4;
                        sample = new LiveMouseSample { TimestampUtc = DateTime.UtcNow, SpeedX = speed, SpeedY = speed };
                        form.UpdateLiveSample(sample, DateTime.UtcNow);
                        Snapshot(form, Path.Combine("demo-frames", "frame-" + i.ToString("00") + ".png"));
                    }
                }
                return 0;
            }
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
            if (Array.IndexOf(args, "--live-lut-roundtrip") >= 0)
            {
                var before = DriverConfig.GetActive();
                var beforeJson = before.ToJSON();
                File.WriteAllText("live-lut-before.json", beforeJson);
                // Prevent startup settings from changing the configuration before the test.
                File.WriteAllText("settings.json", beforeJson);
                try
                {
                    using (var backend = new RawAcceleration())
                    {
                        Thread.Sleep(1250); Application.DoEvents();
                        var original = CurveProfile.Copy(before.profiles[0]);
                        var curve = new CurveModel(new[] { new CurvePoint(0,1), new CurvePoint(3.12345,1),
                            new CurvePoint(10.23456,1.234567), new CurvePoint(20.34567,1.61),
                            new CurvePoint(40.45678,1.48), new CurvePoint(60.56789,1.8) });
                        var desired = CurveProfile.Copy(original);
                        desired.argsX = CurveProfile.Export(curve, original.argsX);
                        desired.argsY = CurveProfile.Export(curve, original.argsY);
                        var expected = DriverConfig.Convert(beforeJson).Item1; expected.SetProfileAt(0, desired);
                        var task = backend.AccelGUI.Settings.ApplyGraphicalProfileAsync(desired);
                        Pump(() => task.IsCompleted); task.GetAwaiter().GetResult();
                        var actual = DriverConfig.GetActive();
                        File.WriteAllText("live-lut-expected.json", expected.ToJSON());
                        File.WriteAllText("live-lut-readback.json", actual.ToJSON());
                        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(expected.ToJObject(), actual.ToJObject()), "Actual LUT driver readback differs");
                        var saved = DriverConfig.Convert(File.ReadAllText("settings.json")).Item1;
                        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(saved.ToJObject(), actual.ToJObject()), "LUT settings save differs from driver");
                        var sampler = CurveProfile.Sampler(actual.profiles[0].argsX);
                        foreach (var speed in new double[] {.001,3,7,10,17,25,45,60,100,1000})
                            Check(Math.Abs(sampler(speed) - curve.ExportedValue(speed)) < .00003, "Applied LUT differs from plotted curve at " + speed);
                        var restore = backend.AccelGUI.Settings.ApplyGraphicalProfileAsync(original);
                        Pump(() => restore.IsCompleted); restore.GetAwaiter().GetResult();
                        Check(Newtonsoft.Json.Linq.JToken.DeepEquals(before.ToJObject(), DriverConfig.GetActive().ToJObject()), "Transaction restore differs from original");
                    }
                    File.WriteAllText("live-lut-result.txt", "PASS: real edited LUT apply, exact typed driver readback, settings save, 10 native curve samples and original-curve restore. Initial full configuration restored.");
                    return 0;
                }
                finally
                {
                    System.Threading.Thread.Sleep(1200);
                    before.Activate();
                    System.Threading.Thread.Sleep(1200);
                    Check(Newtonsoft.Json.Linq.JToken.DeepEquals(before.ToJObject(), DriverConfig.GetActive().ToJObject()), "Final full-configuration rollback failed");
                    File.WriteAllText("settings.json", beforeJson);
                }
            }
            statePath = Path.Combine(Environment.CurrentDirectory, "test-editor-state.json");
            if(Array.IndexOf(args,"--native-cursor-crash-worker")>=0) {CursorLocatorTests.CrashWorker();return 0;}
            if(Array.IndexOf(args,"--live-native-cursor")>=0) {CursorLocatorTests.LiveNative();return 0;}
            if(Array.IndexOf(args,"--cursor-locator-only")>=0)
            { var count=CursorLocatorTests.Run(); File.WriteAllText("curve-editor-test-result.txt","PASS: "+count+" cursor locator checks. No driver writes or injected mouse input."); return 0; }
            if(Array.IndexOf(args,"--chrome-only")>=0) { WindowChromeTests(); File.WriteAllText("chrome-result.txt","PASS: "+checks); return 0; }
            if (File.Exists(statePath)) File.Delete(statePath);
            File.WriteAllText("test-progress.txt",DateTime.UtcNow.ToString("O")+" start\n");
            Action<string,Action> run=(name,test)=>{test();File.AppendAllText("test-progress.txt",DateTime.UtcNow.ToString("O")+" "+name+"\n");};
            run("model",ModelTests);run("snap",SnapTests);run("editor",EditorTests);run("history",HistoryTests);
            run("theme",LightThemeTests);run("chrome",WindowChromeTests);run("reference",AppliedPointTests);
            run("desktop",DesktopTests);run("cursor-locator",()=>checks+=CursorLocatorTests.Run());run("comparison",ComparisonTests);run("presets",UserPresetTests);
            File.WriteAllText("curve-editor-test-result.txt", "PASS: " + checks + " checks. Native LUT parity, capped tail, maximum points, drag/add/delete, fixed view, preview-only, apply/restore callbacks, per-axis preservation, control-point reload. No live driver writes.");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText("curve-editor-test-result.txt", "FAIL: " + ex); return 1; }
    }
}
