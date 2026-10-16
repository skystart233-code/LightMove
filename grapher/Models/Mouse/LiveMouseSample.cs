using System;

namespace grapher.Models.Mouse
{
    /// <summary>The same estimated input positions used by the original live chart.</summary>
    public sealed class LiveMouseSample
    {
        public DateTime TimestampUtc { get; set; }
        public double SpeedX { get; set; }
        public double SpeedY { get; set; }
        public double MultiplierX { get; set; }
        public double MultiplierY { get; set; }
    }
}
