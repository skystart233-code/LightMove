using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using grapher.Desktop;

internal static class CursorLocatorTests
{
    private static int checks;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process,int flag);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] private static extern int GetWindowStyle(IntPtr window,int index);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32.dll",EntryPoint="LoadCursorW")] private static extern IntPtr LoadCursor(IntPtr module,IntPtr id);
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; }
    private sealed class Startup : IStartupRegistration { public string Command {get;set;} }
    public static int Run()
    {
        var detector=new ShakeDetector(); bool triggered=false;
        for(int i=0;i<12;i++) triggered|=detector.Add(i%2==0?0:40,0,i*32);
        Check(triggered,"Fast horizontal shake is not detected");
        Check(!detector.Add(80,0,384),"Stale shake history prolongs enlargement during a straight movement");
        Check(!detector.Add(120,0,416),"Old direction changes keep refreshing the hold time");
        detector.Reset(); triggered=false;
        for(int i=0;i<12;i++) triggered|=detector.Add(0,i%2==0?0:40,i*32);
        Check(triggered,"Fast vertical shake is not detected");
        detector.Reset(); triggered=false;
        for(int i=0;i<30;i++) triggered|=detector.Add(i*40,i*10,i*32);
        Check(!triggered,"Ordinary cross-screen sweep triggers locator");
        detector.Reset(); triggered=false;
        for(int i=0;i<30;i++) triggered|=detector.Add(i%2==0?0:2,0,i*32);
        Check(!triggered,"Tiny jitter triggers locator");
        detector.Reset(); triggered=false;
        for(int i=0;i<30;i++) triggered|=detector.Add(i%2==0?0:40,0,i*150);
        Check(!triggered,"Slow adjustments trigger locator");
        detector.Reset(); triggered=false;
        for(int i=0;i<30;i++) triggered|=detector.Add(i%2==0?0:40,0,i*32,true);
        Check(!triggered,"Drag suppression is ignored");
        Check(!detector.Add(0,0,1000),"Suppressed samples remain in history");
        detector.Reset(); triggered=false;
        for(int i=0;i<30;i++) triggered|=detector.Add(i%2==0?0:4000,0,i*32);
        Check(!triggered,"Cursor warps trigger locator");
        Check(!detector.Add(double.NaN,0,2000) && !detector.Add(0,double.PositiveInfinity,2010),"Invalid coordinates trigger locator");
        var animation=new ShakeAnimation();
        Check(!animation.Active(0) && animation.Scale(0)==1,"Locator starts enlarged");
        animation.Trigger(0); Check(animation.Scale(0)==1 && animation.Scale(90)==3,"Grow animation fails");
        double shrinking=animation.Scale(160); Check(shrinking>1 && shrinking<3,"Locator does not shrink");
        animation.Trigger(160); Check(Math.Abs(animation.Scale(160)-shrinking)<.00001,"Retrigger jumps during shrink");
        Check(animation.Scale(250)==3,"Retrigger fails to grow back");
        Check(!animation.Active(380) && animation.Scale(380)==1,"Locator remains enlarged after stopping");
        animation.Reset();animation.Trigger(0);Check(!animation.Active(220),"Stop recovery exceeds 220ms");
        animation.Reset(); Check(!animation.Active(1000),"Disabling does not cancel animation");
        using(var locator=new CursorLocator(false))
        { Check(!locator.Enabled,"Disabled locator is running"); locator.SetEnabled(true); locator.SetEnabled(false); Check(!locator.Enabled,"Locator cannot be disabled"); }
        using(var small=CursorPicture.Capture(Cursors.Arrow.Handle,1))
        using(var large=CursorPicture.CaptureStandard(32512,Cursors.Arrow.Handle,3))
        using(var mono=CursorPicture.CaptureStandard(32513,Cursors.IBeam.Handle,3))
        using(var picture=new Bitmap(600,320))
        using(var graphics=Graphics.FromImage(picture))
        {
            Check(large.Image.Width==small.Image.Width*3 && large.Image.Height==small.Image.Height*3,"Pointer is not enlarged 3x");
            Check(Math.Abs(large.Hotspot.X-small.Hotspot.X*3)<=1 && Math.Abs(large.Hotspot.Y-small.Hotspot.Y*3)<=1,"Enlarged hotspot is displaced");
            bool hasInk=false, hasLight=false;
            for(int y=0;y<mono.Image.Height;y++) for(int x=0;x<mono.Image.Width;x++)
            {
                var pixel=mono.Image.GetPixel(x,y);
                if(pixel.A>200 && pixel.R<100 && pixel.G<100 && pixel.B<100) hasInk=true;
                if(pixel.A>200 && pixel.R>160 && pixel.G>160 && pixel.B>160) hasLight=true;
            }
            Check(hasInk && hasLight,"Text cursor has no contrasting outline");
            IntPtr native=large.CreateCursor();
            try
            {
                using(var roundtrip=CursorPicture.Capture(native,1))
                {
                    Check(roundtrip.Image.Size==large.Image.Size && roundtrip.Hotspot==large.Hotspot,"Native cursor loses size or hotspot");
                    Check(roundtrip.Image.GetPixel(roundtrip.Image.Width-1,roundtrip.Image.Height-1).A==0,"Native cursor loses alpha transparency");
                }
            }
            finally {DestroyCursor(native);}
            graphics.Clear(Color.FromArgb(245,245,247)); graphics.DrawImageUnscaled(small.Image,32,60); graphics.DrawImageUnscaled(large.Image,130,36); graphics.DrawImageUnscaled(mono.Image,360,36);
            picture.Save("lightmove-cursor-locator.png");
        }
        var process=System.Diagnostics.Process.GetCurrentProcess(); int resources=GetGuiResources(process.Handle,0);
        for(int i=0;i<80;i++) using(var pointer=CursorPicture.Capture(Cursors.Arrow.Handle,1+(i%3))) { }
        Check(GetGuiResources(process.Handle,0)<=resources+2,"Cursor capture leaks GDI resources");
        using(var guard=new CursorRecoveryGuard())
        {
            var foreground=GetForegroundWindow();
            Check(guard.WaitUntilStarted(),"Native cursor recovery helper cannot start");
            Check(GetForegroundWindow()==foreground,"Recovery helper steals foreground focus");
        }
        Check(typeof(CursorPicture).Assembly.GetType("grapher.Desktop.CursorOverlay")==null,"Duplicate cursor overlay still exists");
        string path="test-locator-preferences.json"; if(File.Exists(path)) File.Delete(path);
        var service=new DesktopSettingsService(new DesktopSettingsStore(path),new Startup(),"LightMove.exe");
        int changes=0; service.Changed+=(s,e)=>changes++;
        service.Save(false,new DesktopPreferences {ShakeToFindCursor=false});
        Check(changes==1 && !new DesktopSettingsStore(path).Load().ShakeToFindCursor,"Locator preference is not persisted");
        File.WriteAllText(path,"{\"CloseToTray\":true}");
        Check(new DesktopSettingsStore(path).Load().ShakeToFindCursor,"Old preferences cannot enable the new default");
        using(var form=new DesktopSettingsForm(service))
        {
            form.Opacity=0; form.ShowInTaskbar=false; form.Show(); Application.DoEvents();
            var toggle=(CheckBox)form.Controls.Find("ShakeToFindCursor",true).Single();
            Check(!toggle.Checked,"Preference page ignores disabled locator"); toggle.Checked=true;
            using(var image=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save("lightmove-preferences.png");}
            ((Button)form.Controls.Find("SaveDesktopSettings",true).Single()).PerformClick();
            Check(service.Current.ShakeToFindCursor && changes==2,"Preference page does not save locator toggle");
        }
        return checks;
    }
    private static string Fingerprint(uint id)
    {
        using(var picture=CursorPicture.Capture(LoadCursor(IntPtr.Zero,new IntPtr(id)),1))
        using(var buffer=new MemoryStream())
        using(var hash=System.Security.Cryptography.SHA256.Create())
        {
            picture.Image.Save(buffer,System.Drawing.Imaging.ImageFormat.Png);
            return picture.Image.Size+" "+picture.Hotspot+" "+BitConverter.ToString(hash.ComputeHash(buffer.ToArray()));
        }
    }
    public static void CrashWorker()
    {
        var effect=new NativeCursorEffect();effect.Apply(3);
        // Deliberately skip Dispose: recovery must work after the owner exits.
        Environment.Exit(0);
    }
    public static void LiveNative()
    {
        var before=new System.Collections.Generic.Dictionary<uint,string>();
        Size originalSize;
        using(var original=CursorPicture.Capture(LoadCursor(IntPtr.Zero,new IntPtr(32512)),1))originalSize=original.Image.Size;
        foreach(uint id in new uint[]{32512,32513,32515,32649}) before.Add(id,Fingerprint(id));
        using(var effect=new NativeCursorEffect())
        {
            try
            {
                effect.Apply(3);
                Check(before[32512]!=Fingerprint(32512),"Native arrow did not actually enlarge");
                using(var enlarged=CursorPicture.Capture(LoadCursor(IntPtr.Zero,new IntPtr(32512)),1))
                {
                    Check(enlarged.Image.Width==originalSize.Width*3 && enlarged.Image.Height==originalSize.Height*3,"System cursor size differs from requested enlargement");
                    enlarged.Image.Save("lightmove-native-arrow.png");
                }
            }
            finally {effect.Restore();}
            foreach(var item in before) Check(Fingerprint(item.Key)==item.Value,"Normal restore changes cursor "+item.Key);
            effect.Apply(3);Thread.Sleep(900);
            foreach(var item in before) Check(Fingerprint(item.Key)==item.Value,"Watchdog does not recover a stalled cursor "+item.Key);
        }
        var executable=System.Reflection.Assembly.GetExecutingAssembly().Location;
        using(var worker=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable,"--native-cursor-crash-worker")
            {UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden}))
        {
            Check(worker.WaitForExit(10000) && worker.ExitCode==0,"Unexpected-exit worker failed");
            Thread.Sleep(400);
            foreach(var item in before) Check(Fingerprint(item.Key)==item.Value,"Watchdog does not recover an exited owner "+item.Key);
        }
        File.WriteAllText("live-native-cursor-result.txt","PASS: "+checks+" real native cursor checks: actual enlargement, exact normal restore, stalled-owner restore, unexpected process exit restore. Arrow, text caret, crosshair and hand fingerprints restored; no driver writes.");
    }
}
