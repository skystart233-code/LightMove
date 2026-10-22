// Native cursor replacement / restoration adapted from 0cch/ShakeToFindCursor (MIT).
// Copyright (c) 2024 0CCh. See third-party/ShakeToFindCursor/LICENSE.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace grapher.Desktop
{
    public sealed class CursorSnapshot : IDisposable
    {
        public static readonly uint[] Ids={32512,32513,32514,32515,32516,32642,32643,32644,32645,32646,32648,32649,32650};
        private readonly Dictionary<uint,IntPtr> originals=new Dictionary<uint,IntPtr>();
        public IntPtr this[uint id] => originals[id];
        public static CursorSnapshot Capture()
        {
            var snapshot=new CursorSnapshot();
            try
            {
                foreach(uint id in Ids)
                {
                    IntPtr source=CursorNative.LoadCursor(IntPtr.Zero,new IntPtr(id));
                    IntPtr copy=CursorNative.CopyImage(source,2,0,0,0);
                    if(copy==IntPtr.Zero) throw new Win32Exception();
                    snapshot.originals.Add(id,copy);
                }
                return snapshot;
            }
            catch { snapshot.Dispose(); throw; }
        }
        public void Restore()
        {
            Exception failure=null;
            foreach(var original in originals)
            {
                IntPtr copy=CursorNative.CopyImage(original.Value,2,0,0,0);
                if(copy==IntPtr.Zero) {failure=new Win32Exception();continue;}
                if(!CursorNative.SetSystemCursor(copy,original.Key))
                {failure=new Win32Exception();CursorNative.DestroyCursor(copy);}
            }
            if(failure!=null) throw failure;
        }
        public void Dispose() {foreach(var cursor in originals.Values) CursorNative.DestroyCursor(cursor);originals.Clear();}
    }

    public sealed class NativeCursorEffect : IDisposable
    {
        private readonly CursorRecoveryGuard guard;
        private CursorSnapshot originals;
        private readonly Dictionary<uint,CursorPicture> masters=new Dictionary<uint,CursorPicture>();
        private int lastStep=-1;
        public bool Active => originals!=null;
        public NativeCursorEffect() {guard=new CursorRecoveryGuard();}
        public void Apply(double scale)
        {
            int step=(int)Math.Round(Math.Max(1,Math.Min(3,scale))*16);
            if(step<=16) {Restore();return;}
            if(originals==null)
            {
                originals=CursorSnapshot.Capture();
                try
                {
                    foreach(uint id in CursorSnapshot.Ids) masters.Add(id,CursorPicture.CaptureStandard(id,originals[id],3));
                    guard.Prepare();
                }
                catch {Clear();throw;}
            }
            guard.Pulse();
            if(lastStep==step)return;
            foreach(uint id in CursorSnapshot.Ids)
            {
                IntPtr frame=masters[id].CreateCursor(step/48.0);
                if(!CursorNative.SetSystemCursor(frame,id))
                { CursorNative.DestroyCursor(frame);throw new Win32Exception(); }
            }
            lastStep=step;
        }
        public void Restore()
        {
            if(originals==null)return;
            // Disarm only after every original cursor is restored successfully.
            originals.Restore(); guard.Disarm(); Clear();
        }
        private void Clear()
        {foreach(var picture in masters.Values) picture.Dispose();masters.Clear();originals?.Dispose();originals=null;lastStep=-1;}
        public void Dispose()
        {
            try {Restore();}
            finally {guard.Dispose();Clear();}
        }
    }

    // This helper has no window, driver access, input hooks, or network access.
    // It keeps its own copies so a stalled/exited UI cannot leave giant cursors behind.
    public sealed class CursorRecoveryGuard : IDisposable
    {
        private readonly EventWaitHandle stop,prepare,ready,started,armed,pulse,recovered;
        private readonly Process process;
        private bool disposed;
        public CursorRecoveryGuard()
        {
            string token=Guid.NewGuid().ToString("N"), prefix="Local\\LightMoveCursor-"+token+"-";
            stop=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"stop");
            prepare=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+"prepare");
            ready=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"ready");
            started=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"started");
            armed=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"armed");
            pulse=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+"pulse");
            recovered=new EventWaitHandle(false,EventResetMode.ManualReset,prefix+"recovered");
            string file=Path.Combine(Path.GetDirectoryName(typeof(CursorRecoveryGuard).Assembly.Location),"LightMove.CursorGuard.exe");
            try
            {
                using(var parent=Process.GetCurrentProcess())
                    process=Process.Start(new ProcessStartInfo(file,"--cursor-recovery "+parent.Id+" "+parent.StartTime.ToUniversalTime().Ticks+" "+token)
                    {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(file)});
            }
            catch {Dispose();throw;}
        }
        public void Prepare()
        {
            if(!started.WaitOne(1500) || process.HasExited) throw new InvalidOperationException("指针恢复助手未能启动。");
            ready.Reset(); recovered.Reset(); prepare.Set();
            if(!ready.WaitOne(500) || process.HasExited) throw new InvalidOperationException("指针备份未能完成。");
            pulse.Set();armed.Set();
        }
        public bool WaitUntilStarted(int milliseconds=1500) => started.WaitOne(milliseconds) && !process.HasExited;
        public void Pulse()
        {
            if(process.HasExited || recovered.WaitOne(0)) throw new InvalidOperationException("指针已自动恢复，摇晃功能已暂停。");
            pulse.Set();
        }
        public void Disarm() {armed.Reset();}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            stop?.Set();
            try {if(process!=null && !process.HasExited) process.WaitForExit(1000);}
            finally
            {
                process?.Dispose();stop?.Dispose();prepare?.Dispose();ready?.Dispose();started?.Dispose();armed?.Dispose();pulse?.Dispose();recovered?.Dispose();
            }
        }
        public static void Run(string[] args)
        {
            if(args.Length!=4 || args[0]!="--cursor-recovery" || !int.TryParse(args[1],out int parentId) ||
                !long.TryParse(args[2],out long parentStart) || !Guid.TryParseExact(args[3],"N",out var token))return;
            string prefix="Local\\LightMoveCursor-"+token.ToString("N")+"-";
            try
            {
                using(var parent=Process.GetProcessById(parentId))
                using(var stop=EventWaitHandle.OpenExisting(prefix+"stop"))
                using(var prepare=EventWaitHandle.OpenExisting(prefix+"prepare"))
                using(var ready=EventWaitHandle.OpenExisting(prefix+"ready"))
                using(var started=EventWaitHandle.OpenExisting(prefix+"started"))
                using(var armed=EventWaitHandle.OpenExisting(prefix+"armed"))
                using(var pulse=EventWaitHandle.OpenExisting(prefix+"pulse"))
                using(var recovered=EventWaitHandle.OpenExisting(prefix+"recovered"))
                {
                    if(parent.StartTime.ToUniversalTime().Ticks!=parentStart)return;
                    CursorSnapshot snapshot=null;var age=Stopwatch.StartNew();started.Set();
                    try
                    {
                        while(!parent.HasExited)
                        {
                            int signal=WaitHandle.WaitAny(new WaitHandle[]{stop,prepare,pulse},100);
                            if(signal==0)break;
                            if(signal==1 && !armed.WaitOne(0))
                            {snapshot?.Dispose();snapshot=CursorSnapshot.Capture();age.Restart();ready.Set();}
                            if(signal==2)age.Restart();
                            if(armed.WaitOne(0) && age.ElapsedMilliseconds>600)
                            {snapshot?.Restore();armed.Reset();recovered.Set();}
                        }
                    }
                    finally
                    {if(armed.WaitOne(0))snapshot?.Restore();snapshot?.Dispose();}
                }
            }
            catch { /* No driver or configuration writes; parent refuses to arm a failed helper. */ }
        }
    }
}
