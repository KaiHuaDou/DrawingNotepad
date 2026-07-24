using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace InkCanvasNext;

public static class Geometry
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
    public static double MaxDistance2(ref Span<Point> positions)
    {
        double max = 0;
        for (var i = 0; i < positions.Length; i++)
        {
            for (var j = i + 1; j < positions.Length; j++)
            {
                var d2 = Distance2(positions[i], positions[j]);

                if (d2 > max)
                {
                    max = d2;
                }
            }
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Midpoint(Point a, Point b)
    {
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }
}
