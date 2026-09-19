using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

internal static class Geometry
{
    public const double RadToDeg = 180.0 / Math.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AngleFrom(Point center, Point p)
    {
        return Math.Atan2(p.Y - center.Y, p.X - center.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNearIdentity(Matrix m)
    {
        return Math.Abs(m.M11 - 1) < 1e-6
            && Math.Abs(m.M22 - 1) < 1e-6
            && Math.Abs(m.M12) < 1e-6
            && Math.Abs(m.M21) < 1e-6
            && Math.Abs(m.OffsetX) < 1e-6
            && Math.Abs(m.OffsetY) < 1e-6;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Near(Point a, Point b, double radius)
    {
        return Distance2(a, b) <= radius * radius;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StylusPoint ToStylusPoint(Point p)
    {
        return new StylusPoint(p.X, p.Y);
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Point MidPoint(Point a, Point b)
    {
        return new Point((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
    }
}
