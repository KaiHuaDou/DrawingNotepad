using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace InkCanvasNext;

internal static class Geometry
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Distance(Point a, Point b)
    {
        return Math.Sqrt(Distance2(a, b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double Distance2(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
