using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace grapher.CurveEditing
{
    // Save the return path before writing a neutral profile. A crash or restart
    // during comparison must never turn the user's carefully tuned curve into 1x.
    public sealed class FeelComparison
    {
        private sealed class Recovery
        {
            public string EnabledProfile { get; set; }
            public string NeutralSignature { get; set; }
        }
        private readonly string path;
        public bool Paused { get; private set; }
        public Profile EnabledProfile { get; private set; }
        public string RecoveryError { get; private set; }

        public FeelComparison(Profile active, string path)
        {
            this.path = path; EnabledProfile = CurveProfile.Copy(active);
            try
            {
                if (!File.Exists(path)) return;
                var recovery = JsonConvert.DeserializeObject<Recovery>(File.ReadAllText(path));
                if (recovery == null || recovery.NeutralSignature != CurveProfile.Signature(active)) return;
                var saved = DriverConfig.Convert(recovery.EnabledProfile);
                if (saved.Item1 == null || saved.Item1.Errors() != null) throw new IOException("对比前的曲线备份无法读取。");
                EnabledProfile = CurveProfile.Copy(saved.Item1.profiles[0]); Paused = true;
            }
            catch { RecoveryError = "对比备份无法读取，请先检查曲线再切换。"; }
        }
        public static Profile Neutral(Profile enabled) => new Profile { name = enabled.name };
        public async Task ToggleAsync(Func<Profile, Task> apply)
        {
            if (RecoveryError != null) throw new IOException(RecoveryError);
            if (Paused)
            {
                await apply(CurveProfile.Copy(EnabledProfile));
                Paused = false; RemoveRecovery();
            }
            else
            {
                var neutral = Neutral(EnabledProfile);
                var recovery = new Recovery { EnabledProfile = CurveProfile.Signature(EnabledProfile), NeutralSignature = CurveProfile.Signature(neutral) };
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(recovery));
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                await apply(neutral);
                Paused = true;
            }
        }
        public void Applied(Profile profile)
        {
            EnabledProfile = CurveProfile.Copy(profile); Paused = false; RecoveryError = null; RemoveRecovery();
        }
        private void RemoveRecovery()
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* A stale journal is ignored unless the active neutral signature matches. */ }
        }
    }
}
