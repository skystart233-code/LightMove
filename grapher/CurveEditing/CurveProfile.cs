using System;
using System.Linq;

namespace grapher.CurveEditing
{
    public static class CurveProfile
    {
        public static Profile Copy(Profile profile) => DriverConfig.Convert(DriverConfig.FromProfile(profile).ToJSON()).Item1.profiles[0];
        public static string Signature(Profile profile) => DriverConfig.FromProfile(profile).ToJSON();
        public static Func<double, double> Sampler(AccelArgs args)
        {
            // Edit the intrinsic curve. Retain output DPI, rotation, and directional
            // weights on the original profile rather than baking them into the LUT.
            var isolated = new Profile { argsX = args, argsY = args };
            var accel = new ManagedAccel(isolated);
            return speed =>
            {
                int counts = Math.Max(1, (int)Math.Round(Math.Max(.001, speed) * 10000));
                return accel.Accelerate(counts, 0, 1, 10000).Item1 / counts;
            };
        }
        public static CurveModel Import(AccelArgs args)
        {
            var sampler = Sampler(args);
            double max = 60;
            if (args.mode == AccelMode.lut && args.length >= 4)
                max = Math.Max(10, Math.Min(1000, args.data[args.length - 2]));
            var speeds = new double[] { 0, max * .05, max / 6, max / 3, max * 2 / 3, max };
            return new CurveModel(speeds.Select(x => new CurvePoint(x, Math.Max(.1, Math.Min(16, sampler(x))))));
        }
        public static AccelArgs Export(CurveModel curve, AccelArgs original)
        {
            var result = original;
            result.mode = AccelMode.lut;
            result.gain = false; // LUT values represent sensitivity, not output speed.
            result.data = new float[AccelArgs.MaxLutPoints * 2];
            var points = curve.Export();
            if (points.Count > AccelArgs.MaxLutPoints) throw new ArgumentException("控制点过多。");
            result.length = points.Count * 2;
            for (int i = 0; i < points.Count; i++)
            {
                result.data[i * 2] = (float)points[i].X;
                result.data[i * 2 + 1] = (float)points[i].Y;
            }
            return result;
        }
    }
}
