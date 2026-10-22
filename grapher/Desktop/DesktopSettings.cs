using System;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace grapher.Desktop
{
    public sealed class DesktopPreferences
    {
        public bool StartInTray { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool ShakeToFindCursor { get; set; } = true;
    }
    public interface IStartupRegistration { string Command { get; set; } }
    public sealed class WindowsStartupRegistration : IStartupRegistration
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public string Command
        {
            get { using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key?.GetValue("LightMove") as string; }
            set
            {
                using(var key=Registry.CurrentUser.CreateSubKey(Key))
                    if(value==null) key.DeleteValue("LightMove",false); else key.SetValue("LightMove",value,RegistryValueKind.String);
            }
        }
    }
    public class DesktopSettingsStore
    {
        public string Path { get; }
        public DesktopSettingsStore(string path) { Path=path; }
        public DesktopPreferences Load()
        {
            try { return JsonConvert.DeserializeObject<DesktopPreferences>(File.ReadAllText(Path)) ?? new DesktopPreferences(); }
            catch { return new DesktopPreferences(); }
        }
        public virtual void Save(DesktopPreferences preferences)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path)));
            string temporary=Path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temporary,JsonConvert.SerializeObject(preferences,Formatting.Indented));
                if(File.Exists(Path)) File.Replace(temporary,Path,null); else File.Move(temporary,Path);
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
    public sealed class DesktopSettingsService
    {
        private readonly DesktopSettingsStore store;
        private readonly IStartupRegistration startup;
        private readonly string executable;
        public DesktopPreferences Current { get; private set; }
        public event EventHandler Changed;
        public bool AutoStart => !string.IsNullOrWhiteSpace(startup.Command);
        public DesktopSettingsService(DesktopSettingsStore store,IStartupRegistration startup,string executable)
        { this.store=store; this.startup=startup; this.executable=executable; Current=store.Load(); }
        public static DesktopSettingsService CreateDefault() => new DesktopSettingsService(
            new DesktopSettingsStore(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LightMove","preferences.json")),
            new WindowsStartupRegistration(),System.Windows.Forms.Application.ExecutablePath);
        public string StartupCommand => "\""+executable+"\" --background";
        public void RefreshStartupTarget()
        {
            // Only migrate an existing LightMove registration. Never enable it
            // implicitly, and never change Windows StartupApproved overrides.
            if(AutoStart && startup.Command!=StartupCommand) startup.Command=StartupCommand;
        }
        public void Save(bool autoStart,DesktopPreferences next)
        {
            string before=startup.Command;
            try { startup.Command=autoStart ? StartupCommand : null; store.Save(next); Current=next; }
            catch(Exception error)
            {
                try { startup.Command=before; }
                catch(Exception rollback) { throw new InvalidOperationException("设置保存失败，开机启动也未能恢复，请重新检查启动设置。",new AggregateException(error,rollback)); }
                throw;
            }
            Changed?.Invoke(this,EventArgs.Empty);
        }
        public void SetShakeToFindCursor(bool enabled)
        {
            var next=new DesktopPreferences {StartInTray=Current.StartInTray,CloseToTray=Current.CloseToTray,ShakeToFindCursor=enabled};
            store.Save(next);Current=next;Changed?.Invoke(this,EventArgs.Empty);
        }
    }
}
