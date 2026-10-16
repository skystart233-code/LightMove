using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace grapher.CurveEditing
{
    public sealed class DriverSetupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public sealed class FirstRunForm : StudioForm
    {
        private readonly Label detail;
        private readonly Button install, restart;
        private readonly System.Windows.Forms.Timer poll;
        public bool ReadyToOpen { get; private set; }
        private static string ResultPath => Path.Combine(Application.StartupPath, "driver-install-result.json");
        public FirstRunForm(string reason)
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Text = "欢迎使用轻移 LightMove"; ClientSize = new Size(660, 440);
            Font = new Font(LightTheme.FontFamily, 11); BackColor = LightTheme.Page; ForeColor = LightTheme.Ink;
            StartPosition = FormStartPosition.CenterScreen; Resizable = false; MaximizeBox = false;
            ModernWindow.Attach(this);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(30), ColumnCount = 1, RowCount = 6 };
            foreach (int h in new[] { 54, 61, 115, 55, 55, 0 }) layout.RowStyles.Add(h == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h));
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "更顺手，从这里开始。", Font = new Font(Font.FontFamily, 22, FontStyle.Bold), AutoSize = true });
            layout.Controls.Add(new Label { Text = "首次使用需要安装官方鼠标驱动，然后重启一次。\n之后就可以慢移精调、快移跨屏，用红点观察自己的手感。", Dock = DockStyle.Fill });
            detail = new Label { Dock = DockStyle.Fill, ForeColor = LightTheme.Secondary, Text = "尚未检测到可用的鼠标驱动。\n\n点击下方按钮开始安装，并完成 Windows 管理员授权。\n安装完成后，保存工作并重启一次即可开始。" };
            var detailPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); detailPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            detailPanel.Controls.Add(detail, 0, 0);
            var technical = new LinkLabel { Text = "查看检测详情", AutoSize = true, LinkColor = LightTheme.Accent };
            technical.LinkClicked += (s,e) => MessageBox.Show(this, reason, "驱动检测详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
            detailPanel.Controls.Add(technical, 0, 1); layout.Controls.Add(detailPanel);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            install = Button("安装所需驱动", LightTheme.Accent);
            restart = Button("保存工作后重启", LightTheme.SurfaceAlt); restart.Enabled = false;
            var retry = Button("重新检测并打开", LightTheme.SurfaceAlt);
            actions.Controls.AddRange(new Control[] { install, restart, retry }); layout.Controls.Add(actions);
            layout.Controls.Add(new Label { Text = "社区体验版 · 基于 Raw Accel\n保留官方签名驱动与 MIT 许可证。软件不会自动重启电脑。", Dock = DockStyle.Fill, ForeColor = LightTheme.Secondary, Font = new Font(Font.FontFamily, 10) });
            var runtime = new LinkLabel { Text = "缺少运行库？打开 Microsoft 官方安装页面", AutoSize = true, LinkColor = LightTheme.Accent };
            runtime.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo("https://aka.ms/vs/17/release/vc_redist.x64.exe") { UseShellExecute = true }); layout.Controls.Add(runtime);
            poll = new System.Windows.Forms.Timer { Interval = 500 };
            poll.Tick += (s, e) =>
            {
                if (!File.Exists(ResultPath)) return;
                try
                {
                    var result = JsonConvert.DeserializeObject<DriverSetupResult>(File.ReadAllText(ResultPath));
                    poll.Stop(); install.Enabled = !result.Success; restart.Enabled = result.Success;
                    detail.Text = result.Success ? "安装完成。\n\n请先保存工作，再重启电脑。重启后双击轻移，即可开始调节。" : "安装未完成：\n" + result.Message;
                }
                catch (IOException) { }
            };
            install.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(ResultPath)) File.Delete(ResultPath);
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--install-driver-helper") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
                    install.Enabled = false; detail.Text = "正在安装官方驱动，请完成 Windows 管理员授权。"; poll.Start();
                }
                catch (Exception ex) { detail.Text = "安装尚未开始：" + ex.Message; }
            };
            retry.Click += (s, e) =>
            {
                try { VersionHelper.ValidOrThrow(); ReadyToOpen = true; Close(); }
                catch (Exception ex) { detail.Text = "驱动还没有就绪。若刚安装，请先重启电脑。\n\n" + ex.Message; }
            };
            restart.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "请先保存所有工作。现在重启电脑吗？", "重启以启用驱动", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), "/r /t 0") { UseShellExecute = false, CreateNoWindow = true });
            };
            FormClosed += (s, e) => poll.Stop();
            using (var display = CreateGraphics())
                Scale(new SizeF(display.DpiX / 96f, display.DpiY / 96f));
            ResumeLayout(true);
        }
        private Button Button(string text, Color color) => new StudioButton { Text = text, Width = 185, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = color, ForeColor = color == LightTheme.Accent ? LightTheme.AccentInk : ForeColor, Margin = new Padding(0, 0, 10, 0) };
        protected override void Dispose(bool disposing) { if (disposing) poll?.Dispose(); base.Dispose(disposing); }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }
        public static void InstallDriverHelper()
        {
            var result = new DriverSetupResult();
            try
            {
                string root = Application.StartupPath, installer = Path.Combine(root, "installer.exe");
                string source = Path.Combine(root, "driver", "rawaccel.sys");
                const string driverHash = "8A62C4DEEF2774B43A7363B352EDA79897533A1080C9C26FFEFF0559E43358D7";
                if (Hash(source) != driverHash || Hash(installer) != "B3B4948DE9470A5727390ED016507B59D2C628463E42AAF1B75E906EE04F58A9")
                    throw new ApplicationException("驱动或安装器与官方发布包不一致，请重新获取完整软件包。");
                using (var process = Process.Start(new ProcessStartInfo(installer) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    var deadline = DateTime.UtcNow.AddSeconds(20);
                    while (DateTime.UtcNow < deadline)
                    {
                        using (var service = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\rawaccel"))
                        using (var mouse = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96f-e325-11ce-bfc1-08002be10318}"))
                        {
                            var filters = mouse?.GetValue("UpperFilters") as string[];
                            string target = Path.Combine(Environment.SystemDirectory, "drivers", "rawaccel.sys");
                            if (service != null && filters != null && filters.Contains("rawaccel") && filters.Contains("mouclass") && File.Exists(target) && Hash(target) == driverHash)
                            { result.Success = true; break; }
                        }
                        if (process.HasExited) break;
                        Thread.Sleep(200);
                    }
                    if (!process.WaitForExit(500)) { process.Kill(); process.WaitForExit(1000); }
                    result.Message = result.Success ? "官方驱动已安装，需要重启。" : process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                }
            }
            catch (Exception ex) { result.Message = ex.Message; }
            File.WriteAllText(ResultPath, JsonConvert.SerializeObject(result));
        }
    }
}
