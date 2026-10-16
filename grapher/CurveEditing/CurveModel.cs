using System;
using System.Collections.Generic;
using System.Linq;

namespace grapher.CurveEditing
{
    public sealed class CurvePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public CurvePoint() { }
        public CurvePoint(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Shape-preserving cubic interpolation, exported as the driver's linear LUT.</summary>
    public sealed class CurveModel
    {
        public const int MaxPoints = 12;
        public const double Gap = 0.25;
        public List<CurvePoint> Points { get; }
        public CurveModel(IEnumerable<CurvePoint> points)
        {
            Points = points.Select(p => new CurvePoint(p.X, p.Y)).ToList();
            if (Points.Count < 2 || Points.Count > MaxPoints || Points[0].X != 0)
                throw new ArgumentException("曲线需要 2 至 12 个控制点，首点速度为 0。");
            for (int i = 0; i < Points.Count; i++)
                if (!Finite(Points[i].X) || Points[i].X > 10000 || !Finite(Points[i].Y) || Points[i].Y < .1 || Points[i].Y > 16 ||
                    (i > 0 && Points[i].X - Points[i - 1].X < Gap))
                    throw new ArgumentException("控制点坐标无效或过于接近。");
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public CurveModel Copy() => new CurveModel(Points);
        public void Move(int index, double x, double y, double maxX, double maxY)
        {
            if (index < 0 || index >= Points.Count) return;
            if (!Finite(x) || !Finite(y)) return;
            double min = index == 0 ? 0 : Points[index - 1].X + Gap;
            double max = index == Points.Count - 1 ? maxX : Points[index + 1].X - Gap;
            Points[index].X = index == 0 ? 0 : Math.Max(min, Math.Min(max, x));
            Points[index].Y = Math.Max(.1, Math.Min(Math.Min(16, maxY), y));
        }
        public int Add(double x, double y)
        {
            if (!Finite(x) || !Finite(y) || Points.Count >= MaxPoints || x < Gap ||
                x >= Points.Last().X || Points.Any(p => Math.Abs(p.X - x) < Gap)) return -1;
            var index = Points.FindIndex(p => p.X > x);
            Points.Insert(index, new CurvePoint(x, Math.Max(.1, Math.Min(16, y))));
            return index;
        }
        public bool Delete(int index)
        {
            if (Points.Count <= 2 || index <= 0 || index >= Points.Count - 1) return false;
            Points.RemoveAt(index); return true;
        }
        private double[] Tangents()
        {
            int n = Points.Count;
            var slopes = new double[n - 1]; var widths = new double[n - 1]; var tangents = new double[n];
            for (int i = 0; i < n - 1; i++)
            {
                widths[i] = Points[i + 1].X - Points[i].X;
                slopes[i] = (Points[i + 1].Y - Points[i].Y) / widths[i];
            }
            // Flat endpoint tangents make the low/high-speed plateaus continuous.
            for (int i = 1; i < n - 1; i++)
            {
                if (slopes[i - 1] * slopes[i] <= 0) continue;
                double w1 = 2 * widths[i] + widths[i - 1];
                double w2 = widths[i] + 2 * widths[i - 1];
                tangents[i] = (w1 + w2) / (w1 / slopes[i - 1] + w2 / slopes[i]);
            }
            return tangents;
        }
        public double Evaluate(double x)
        {
            if (x <= 0) return Points[0].Y;
            if (x >= Points.Last().X) return Points.Last().Y;
            int i = 0;
            while (x > Points[i + 1].X) i++;
            var t = Tangents();
            double width = Points[i + 1].X - Points[i].X;
            double u = (x - Points[i].X) / width, u2 = u * u, u3 = u2 * u;
            return (2 * u3 - 3 * u2 + 1) * Points[i].Y + (u3 - 2 * u2 + u) * width * t[i] +
                (-2 * u3 + 3 * u2) * Points[i + 1].Y + (u3 - u2) * width * t[i + 1];
        }
        public List<CurvePoint> Export()
        {
            var result = new List<CurvePoint>();
            // v1.7.1's signed wrapper compares twice the LUT length when deciding
            // whether X/Y match. Keep <=128 pairs so its comparison stays in bounds.
            int subdivisions = Math.Min(16, 126 / (Points.Count - 1));
            for (int i = 0; i < Points.Count - 1; i++)
                for (int j = 0; j < subdivisions; j++)
                {
                    double x = Points[i].X + (Points[i + 1].X - Points[i].X) * j / subdivisions;
                    x = Math.Max(.001, x);
                    result.Add(new CurvePoint(x, Evaluate(x)));
                }
            var last = Points.Last();
            result.Add(new CurvePoint(last.X, last.Y));
            // The driver extrapolates its last segment. A flat sentinel prevents
            // unbounded acceleration beyond the visible endpoint.
            result.Add(new CurvePoint(last.X + Math.Max(1, last.X * .05), last.Y));
            return result;
        }
        public double ExportedValue(double x)
        {
            return Sample(Export(), x);
        }
        public static double Sample(IList<CurvePoint> table, double x)
        {
            if (x <= table[0].X) return table[0].Y;
            for (int i = 1; i < table.Count; i++)
                if (x <= table[i].X)
                {
                    double fraction = (x - table[i - 1].X) / (table[i].X - table[i - 1].X);
                    return table[i - 1].Y + fraction * (table[i].Y - table[i - 1].Y);
                }
            return table.Last().Y;
        }
    }
}
