using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace InkCanvasNext;

internal static class Geometry
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(Point a, Point b)
    {
        return Math.Sqrt(Distance2(a, b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance2(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Midpoint(Point a, Point b)
    {
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }
}
