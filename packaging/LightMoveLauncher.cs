using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using grapher.CurveEditing;

[assembly: AssemblyTitle("轻移 LightMove")]
[assembly: AssemblyDescription("轻移中文桌面体验版启动器，基于 Raw Accel")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]

internal static class LightMoveLauncher
{
    private static Stream Payload() => Assembly.GetExecutingAssembly().GetManifestResourceStream("LightMove.Payload.zip");
    private static string PayloadHash()
    {
        using (var sha = SHA256.Create()) using (var stream = Payload())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static void Extract(string destination)
    {
        string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);
        using (var payload = Payload()) using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
        {
            foreach (var entry in archive.Entries)
            {
                string path = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("软件包路径无效。");
                if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var input = entry.Open()) using (var output = File.Create(path)) input.CopyTo(output);
            }
        }
        if (!File.Exists(Path.Combine(root, "LightMove.exe")) || !File.Exists(Path.Combine(root, "driver", "rawaccel.sys")))
            throw new IOException("软件包不完整。");
    }
    private static string Prepare()
    {
        string hash = PayloadHash();
        string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LightMove");
        string destination = Path.Combine(parent, "preview-" + hash.Substring(0, 12));
        using (var mutex = new Mutex(false, "Local\\LightMoveUnpack"))
        {
            try { mutex.WaitOne(); } catch (AbandonedMutexException) { }
            try
            {
                string marker = Path.Combine(destination, ".package-ready");
                if (!File.Exists(marker) || File.ReadAllText(marker) != hash || !File.Exists(Path.Combine(destination, "LightMove.exe")))
                {
                    var previous = Directory.Exists(parent) ? Directory.GetDirectories(parent)
                        .Where(d => !string.Equals(d, destination, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(d, ".package-ready")))
                        .OrderByDescending(Directory.GetLastWriteTimeUtc).FirstOrDefault() : null;
                    Extract(destination);
                    if (previous != null)
                        foreach (var name in new[] { "settings.json", ".config", ".curve-editor.json" })
                            if (File.Exists(Path.Combine(previous, name))) File.Copy(Path.Combine(previous, name), Path.Combine(destination, name), true);
                    File.WriteAllText(marker, hash);
                }
                Directory.SetLastWriteTimeUtc(destination, DateTime.UtcNow);
            }
            finally { mutex.ReleaseMutex(); }
        }
        return destination;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        bool extractOnly = args.Length == 2 && args[0] == "--extract-only";
        try
        {
            if (extractOnly) { Extract(args[1]); return 0; }
            if (args.Length == 1 && args[0] == "--prepare-only")
            {
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lightmove-prepared-path.txt"), Prepare());
                return 0;
            }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var splash = new Form())
            {
                splash.SuspendLayout(); splash.AutoScaleMode = AutoScaleMode.None;
                splash.Text = "轻移 LightMove"; splash.ClientSize = new Size(410, 190);
                splash.BackColor = LightTheme.Page; splash.ForeColor = LightTheme.Ink;
                splash.Font = new Font(LightTheme.FontFamily, 11); splash.StartPosition = FormStartPosition.CenterScreen;
                splash.FormBorderStyle = FormBorderStyle.FixedDialog; splash.ControlBox = false;
                splash.Controls.Add(new Label { Text = "轻移 LightMove", Font = new Font(splash.Font.FontFamily, 23, FontStyle.Bold), AutoSize = true, Location = new Point(28, 26) });
                splash.Controls.Add(new Label { Text = "正在准备更顺手的操作…", AutoSize = true, Location = new Point(30, 87), ForeColor = LightTheme.Secondary });
                splash.Controls.Add(new ProgressBar { Location = new Point(30, 133), Size = new Size(350, 6), Style = ProgressBarStyle.Marquee });
                using (var display = splash.CreateGraphics()) splash.Scale(new SizeF(display.DpiX / 96f, display.DpiY / 96f));
                splash.ResumeLayout(true);
                splash.Shown += async (s, e) =>
                {
                    try
                    {
                        string root = await Task.Run(() => Prepare());
                        string launchArguments=string.Join(" ",args.Where(a=>a=="--check-live" || a=="--background"));
                        Process.Start(new ProcessStartInfo(Path.Combine(root, "LightMove.exe"), launchArguments) { WorkingDirectory = root, UseShellExecute = true });
                    }
                    catch (Exception ex) { MessageBox.Show(splash, ex.Message, "轻移暂时无法打开", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    finally { splash.Close(); }
                };
                Application.Run(splash);
            }
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lightmove-launcher-error.txt"), ex.ToString());
            if (!extractOnly) MessageBox.Show(ex.Message, "轻移暂时无法打开");
            return 1;
        }
    }
}
