using System;

namespace OddWire.System;

public static class MathExtensions
{
    public static bool Approx(this float a, float b, float epsilon = 1e-5f) => MathF.Abs(a - b) <= epsilon;
    public static bool Approx(this double a, double b, double epsilon = 1e-5) => Math.Abs(a - b) <= epsilon;
}
