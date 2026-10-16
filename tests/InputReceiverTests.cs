using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using grapher;
using grapher.Models.Mouse;

// Compiled against isolated source copies with driver reads replaced by defaults
// and activation/reset disabled by run-input-receiver-tests.ps1.
internal static class InputReceiverTests
{
    [DllImport("user32.dll")]
    private static extern uint GetRegisteredRawInputDevices([Out] MouseWatcher.RAWINPUTDEVICE[] devices, ref uint count, uint size);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static IntPtr Target()
    {
        uint count = 0, size = (uint)Marshal.SizeOf(typeof(MouseWatcher.RAWINPUTDEVICE));
        GetRegisteredRawInputDevices(null, ref count, size);
        var devices = new MouseWatcher.RAWINPUTDEVICE[count];
        GetRegisteredRawInputDevices(devices, ref count, size);
        return devices.Single(d => d.UsagePage == MouseWatcher.HIDUsagePage.Generic && d.Usage == MouseWatcher.HIDUsage.Mouse).WindowHandle;
    }
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            IntPtr input;
            using (var backend = new RawAcceleration())
            {
                input = Target();
                Check(input == backend.AccelGUI.MouseWatcher.InputWindowHandle && IsWindow(input), "Receiver not registered");
                var before = backend.Handle;
                typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(backend, null);
                Application.DoEvents();
                Check(before != backend.Handle && Target() == input && IsWindow(input), "Backend HWND recreation disconnects input");
                using (var home = backend.CreateCurveEditor())
                {
                    home.Opacity = 0; home.ShowInTaskbar = false; home.Show(); Application.DoEvents();
                    Check(Target() == input && IsWindow(input), "Opening home disconnects input");
                    home.WindowState = FormWindowState.Minimized; Application.DoEvents();
                    home.WindowState = FormWindowState.Normal; Application.DoEvents();
                    Check(Target() == input && IsWindow(input), "Minimize/restore disconnects input");
                    typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(home, null);
                    Application.DoEvents();
                    Check(Target() == input && IsWindow(input), "Home HWND recreation disconnects input");
                    home.Close();
                    Check(Target() == input && IsWindow(input), "Closing home destroys backend input prematurely");
                }
            }
            Check(!IsWindow(input), "Input window leaks after backend disposal");
            File.WriteAllText("input-receiver-test-result.txt", "PASS: 7 receiver lifecycle checks. Backend/home HWND recreation, minimize/restore, close and disposal. Isolated driver defaults; no live writes or simulated physical input.");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText("input-receiver-test-result.txt", "FAIL: " + ex); return 1; }
    }
}
