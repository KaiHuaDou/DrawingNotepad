using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double Get1stFingerDispl2( )
    {
        if (touches.Count == 0)
        {
            return 0;
        }

        var first = touches.First( );
        if (!touchStarts.TryGetValue(first.Key, out var start))
        {
            return 0;
        }

        var current = first.Value.Position;
        return Geometry.Distance2(current, start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private (Point? First, Point? Second) GetMajorTouches( )
    {
        var values = touches.Values;
        var count = values.Count;

        if (count == 0)
        {
            return (null, null);
        }

        using var enumerator = values.GetEnumerator( );
        enumerator.MoveNext( );
        var first = enumerator.Current.Position;

        if (count == 1)
        {
            return (first, null);
        }

        enumerator.MoveNext( );
        var second = enumerator.Current.Position;

        return (first, second);
    }

    private bool IsFirstTouch(int id)
    {
        if (touches.Count == 0)
        {
            return false;
        }

        return touches.Keys.First( ) == id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private double GetMaxDistance2( )
    {
        var count = touches.Count;
        if (count < 2)
        {
            return 0;
        }

        Span<Point> positions = stackalloc Point[count];
        var index = 0;
        foreach (var kv in touches)
        {
            positions[index++] = kv.Value.Position;
        }

        double max = 0;
        for (var i = 0; i < count - 1; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                var d2 = Geometry.Distance2(positions[i], positions[j]);
                if (d2 > max)
                {
                    max = d2;
                }
            }
        }

        return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private (Point C1, Point C2) GetPinchPoints( )
    {
        using var enumerator = touches.Values.GetEnumerator( );
        enumerator.MoveNext( );
        var (Device1, _) = enumerator.Current;
        enumerator.MoveNext( );
        var (Device2, _) = enumerator.Current;
        return (Device1.GetTouchPoint(InnerCanvas).Position, Device2.GetTouchPoint(InnerCanvas).Position);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private Point GetFirstCanvasPoint( )
    {
        using var enumerator = touches.Values.GetEnumerator( );
        enumerator.MoveNext( );
        return enumerator.Current.Device.GetTouchPoint(InnerCanvas).Position;
    }

    /// <summary>
    /// 触点包络圆：视口坐标下的触点质心与最远距离
    /// </summary>
    private static (Point Center, double Radius) GetEnvelopeCircle(OrderedDictionary<int, (TouchDevice Device, Point Position)> touches)
    {
        if (touches.Count == 0)
        {
            return (default, 1);
        }

        double sumX = 0, sumY = 0;
        foreach (var kv in touches)
        {
            var pos = kv.Value.Position;
            sumX += pos.X;
            sumY += pos.Y;
        }

        var center = new Point(sumX / touches.Count, sumY / touches.Count);

        double max = 0;
        foreach (var kv in touches)
        {
            var distance2 = Geometry.Distance2(kv.Value.Position, center);
            if (distance2 > max)
            {
                max = distance2;
            }
        }

        return (center, Math.Sqrt(max));
    }

    /// <summary>
    /// 触点质心，内容坐标
    /// </summary>
    private Point GetCanvasCenter(OrderedDictionary<int, (TouchDevice Device, Point Position)> touches)
    {
        if (touches.Count == 0)
        {
            return default;
        }

        double sumX = 0, sumY = 0;
        foreach (var kv in touches)
        {
            var pos = kv.Value.Device.GetTouchPoint(InnerCanvas).Position;
            sumX += pos.X;
            sumY += pos.Y;
        }

        return new Point(sumX / touches.Count, sumY / touches.Count);
    }
}
