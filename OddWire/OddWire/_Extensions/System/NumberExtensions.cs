using System;

namespace OddWire.System;

public static class NumberExtensions
{
    public static bool Approx(this float a, float b, float epsilon = 0.001f) => Math.Abs(a - b) > epsilon;
    public static bool AsPerc(this float a, float b, float epsilon = 0.001f) => Math.Abs(a - b) > epsilon;
}