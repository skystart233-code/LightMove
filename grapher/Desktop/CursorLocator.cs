using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace grapher.Desktop
{
    public sealed class CursorLocator : IDisposable
    {
        private readonly Timer timer = new Timer { Interval = 32 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly ShakeDetector detector = new ShakeDetector();
        private readonly ShakeAnimation animation = new ShakeAnimation();
        private NativeCursorEffect effect;
        private bool enabled, disposed;
        public bool Enabled => enabled;
        public string LastError { get; private set; }
        public int TriggerCount { get; private set; }
        public event EventHandler Failed;
        public CursorLocator(bool enabled)
        {
            timer.Tick += Tick;
            SetEnabled(enabled);
        }
        public void SetEnabled(bool value)
        {
            if (disposed) return;
            enabled = value; detector.Reset(); animation.Reset();
            if(value) LastError=null;
            timer.Stop(); effect?.Dispose(); effect=null; timer.Interval = 32;
            if (value)
                try {effect=new NativeCursorEffect();timer.Start();}
                catch(Exception error) {LastError=error.Message;enabled=false;Failed?.Invoke(this,EventArgs.Empty);}
        }
        private void Tick(object sender, EventArgs e)
        {
            try
            {
                var cursor = CursorNative.Read();
                double now = clock.Elapsed.TotalMilliseconds;
                if (!cursor.Visible || CursorNative.ButtonHeld())
                { detector.Reset(); animation.Reset(); effect?.Restore(); timer.Interval = 32; return; }
                if (detector.Add(cursor.Position.X, cursor.Position.Y, now))
                { if(!animation.Active(now)) TriggerCount++; animation.Trigger(now); }
                if (!animation.Active(now)) { effect?.Restore(); timer.Interval = 32; return; }
                timer.Interval = 16;
                effect?.Apply(animation.Scale(now));
            }
            catch (Exception error)
            {
                LastError = error.Message;
                try {SetEnabled(false);} catch { /* Independent recovery helper owns an original snapshot. */ }
                Failed?.Invoke(this,EventArgs.Empty);
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            SetEnabled(false); disposed = true; timer.Dispose(); clock.Stop();
        }
    }
}
