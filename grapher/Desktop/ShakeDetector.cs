// Adapted from 0cch/ShakeToFindCursor, MouseMoveDetector (MIT).
// Copyright (c) 2024 0CCh. See third-party/ShakeToFindCursor/LICENSE.
using System;
using System.Collections.Generic;

namespace grapher.Desktop
{
    // Pure, bounded motion history. Never records screen contents or writes input to disk.
    public sealed class ShakeDetector
    {
        private struct Movement { public double X, Y, Time, Distance; }
        private readonly Queue<Movement> history = new Queue<Movement>();
        private bool initialized;
        private double lastX, lastY, lastTime;
        public void Reset() { history.Clear(); initialized = false; }

        public bool Add(double x, double y, double milliseconds, bool suppressed = false)
        {
            if (suppressed || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(milliseconds) ||
                double.IsInfinity(x) || double.IsInfinity(y) || double.IsInfinity(milliseconds))
            { Reset(); return false; }
            if (!initialized || milliseconds <= lastTime || milliseconds - lastTime > 180)
            { history.Clear(); initialized = true; lastX = x; lastY = y; lastTime = milliseconds; return false; }
            double dx = x - lastX, dy = y - lastY, dt = milliseconds - lastTime;
            // Accumulate tiny moves rather than interpreting sensor jitter as reversals.
            if (Math.Abs(dx) < 6 && Math.Abs(dy) < 6) return false;
            lastX = x; lastY = y; lastTime = milliseconds;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            // Cursor warps / remote-desktop jumps are not a shake.
            if (distance > 1500) { history.Clear(); return false; }
            history.Enqueue(new Movement { X = dx, Y = dy, Time = milliseconds, Distance = distance });
            while (history.Count > 40 || (history.Count > 0 && milliseconds - history.Peek().Time > 500)) history.Dequeue();
            if (history.Count < 6) return false;
            int changesX = 0, changesY = 0, previousX = 0, previousY = 0;
            double travel = 0; bool freshReversal=false;
            foreach (var movement in history)
            {
                int directionX = Math.Abs(movement.X) < 6 ? 0 : Math.Sign(movement.X);
                int directionY = Math.Abs(movement.Y) < 6 ? 0 : Math.Sign(movement.Y);
                freshReversal=false;
                if (directionX != 0) { if (previousX != 0 && previousX != directionX) {changesX++;freshReversal=true;} previousX = directionX; }
                if (directionY != 0) { if (previousY != 0 && previousY != directionY) {changesY++;freshReversal=true;} previousY = directionY; }
                travel += movement.Distance;
            }
            double elapsed = milliseconds - history.Peek().Time + dt;
            // One axis must reverse repeatedly: a diagonal sweep or one corner is insufficient.
            return freshReversal && Math.Max(changesX, changesY) >= 4 && travel >= 120 && elapsed >= 100 && travel / elapsed >= .6;
        }
    }

    public sealed class ShakeAnimation
    {
        private double triggeredAt = double.NegativeInfinity, growAt, growFrom = 1;
        public const double GrowMs=90, HoldMs=100, ShrinkMs=120;
        public bool Active(double now) => now - triggeredAt < HoldMs+ShrinkMs;
        public void Trigger(double now)
        {
            if (!Active(now)) { growFrom = 1; growAt = now; }
            else if (now - triggeredAt > HoldMs) { growFrom = Scale(now); growAt = now; }
            triggeredAt = now;
        }
        public double Scale(double now)
        {
            if (!Active(now)) return 1;
            double grow = Math.Max(0, Math.Min(1, (now - growAt) / GrowMs));
            double scale = growFrom + (3 - growFrom) * (1 - Math.Pow(1 - grow, 3));
            double shrink = Math.Max(0, Math.Min(1, (now - triggeredAt - HoldMs) / ShrinkMs));
            return 1 + (scale - 1) * (1 - shrink * shrink * shrink);
        }
        public void Reset() { triggeredAt = double.NegativeInfinity; growFrom = 1; }
    }
}
