using System;

namespace FamilyMuseum.Decor
{
    public sealed class DecorBounds
    {
        public readonly double minX, minY, minZ, maxX, maxY, maxZ;
        internal DecorBounds(double x, double y, double z, double sx, double sy, double sz)
        { minX = x - sx / 2; maxX = x + sx / 2; minY = y - sy / 2; maxY = y + sy / 2; minZ = z - sz / 2; maxZ = z + sz / 2; }
    }
    public static class DecorGeometry
    {
        public const double ToleranceM = .001;
        const double ArithmeticNoise = 1e-9;
        public static bool SupportedYaw(double yaw) { return yaw == 0 || yaw == 90 || yaw == 180 || yaw == 270; }
        public static double NormalizeYaw(double yaw)
        { Finite(yaw); double result = yaw % 360; return result < 0 ? result + 360 : result; }
        public static double QuantizeM(double value)
        {
            Finite(value); if (Math.Abs(value) > 1e9) throw new ArgumentOutOfRangeException("value");
            return (double)Math.Round((decimal)value, 3, MidpointRounding.AwayFromZero);
        }
        public static double[] SnapPosition(double[] position, double[] origin, double stepM)
        {
            Vector(position); Vector(origin);
            if (stepM != .25 && stepM != .05) throw new ArgumentOutOfRangeException("stepM");
            return new[] { SnapAxis(position[0], origin[0], stepM), QuantizeM(position[1]), SnapAxis(position[2], origin[2], stepM) };
        }
        static double SnapAxis(double value, double origin, double step)
        {
            if (Math.Abs(value) > 1e9 || Math.Abs(origin) > 1e9) throw new ArgumentOutOfRangeException("position");
            // Decimal arithmetic prevents 0.075/0.05 becoming just less than the intended half step.
            decimal result = (decimal)origin + Math.Round(((decimal)value - (decimal)origin) / (decimal)step, 0, MidpointRounding.AwayFromZero) * (decimal)step;
            return QuantizeM((double)result);
        }
        public static double[] TransformPoint(double[] position, double yaw, double[] local)
        {
            Vector(position); Vector(local); if (!SupportedYaw(yaw)) throw new ArgumentOutOfRangeException("yaw");
            double x = local[0], z = local[2];
            if (yaw == 90) { x = local[2]; z = -local[0]; }
            else if (yaw == 180) { x = -local[0]; z = -local[2]; }
            else if (yaw == 270) { x = -local[2]; z = local[0]; }
            return new[] { position[0] + x, position[1] + local[1], position[2] + z };
        }
        public static DecorBounds Bounds(DecorBox box, double[] position, double yaw)
        {
            if (box == null) throw new ArgumentNullException("box"); Vector(box.centerM); Vector(box.sizeM);
            foreach (double n in box.sizeM) if (n <= 0) throw new ArgumentOutOfRangeException("box");
            double[] center = TransformPoint(position, yaw, box.centerM);
            bool turn = yaw == 90 || yaw == 270;
            return new DecorBounds(center[0], center[1], center[2], box.sizeM[turn ? 2 : 0], box.sizeM[1], box.sizeM[turn ? 0 : 2]);
        }
        public static bool Contains(DecorBounds outer, DecorBounds inner)
        {
            if (outer == null || inner == null) throw new ArgumentNullException("bounds");
            return !Exceeds(outer.minX - inner.minX) && !Exceeds(inner.maxX - outer.maxX)
                && !Exceeds(outer.minY - inner.minY) && !Exceeds(inner.maxY - outer.maxY)
                && !Exceeds(outer.minZ - inner.minZ) && !Exceeds(inner.maxZ - outer.maxZ);
        }
        public static bool Overlaps(DecorBounds a, DecorBounds b)
        {
            if (a == null || b == null) throw new ArgumentNullException("bounds");
            // Minimum separating translation, not intersection width: a thin proxy fully inside
            // a larger solid can have a sub-millimeter intersection but still be deeply embedded.
            return Exceeds(Math.Min(a.maxX - b.minX, b.maxX - a.minX))
                && Exceeds(Math.Min(a.maxY - b.minY, b.maxY - a.minY))
                && Exceeds(Math.Min(a.maxZ - b.minZ, b.maxZ - a.minZ));
        }
        internal static bool Exceeds(double distance) { return distance > ToleranceM + ArithmeticNoise; }
        static void Finite(double value) { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException("value"); }
        static void Vector(double[] value) { if (value == null || value.Length != 3) throw new ArgumentException("Expected 3-vector"); foreach (double n in value) Finite(n); }
    }
}
