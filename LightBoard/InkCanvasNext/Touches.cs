using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

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

    /// <summary>指定触点是否为插入序的第一指。</summary>
    private bool IsFirstTouch(int id)
    {
        if (touches.Count == 0)
        {
            return false;
        }

        using var enumerator = touches.Keys.GetEnumerator( );
        enumerator.MoveNext( );
        return enumerator.Current == id;
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
}
