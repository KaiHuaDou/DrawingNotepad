using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InkCanvasNext;

internal enum TouchKind
{
    Finger,
    Palm
}

/// <summary>
/// 触点：位置取根可视坐标，尺寸取 TouchPoint.Size（视口 DIP）；
/// Kind 由尺寸通道在按下时分类，无法上报有效尺寸的设备始终为 Finger
/// </summary>
internal readonly record struct TouchContact(
    TouchDevice Device,
    Point Position,
    Size Size,
    TouchKind Kind)
{
    internal bool IsPalm => Kind == TouchKind.Palm;
}

public partial class InkCanvasNext
{
    // 以下均为只读触点集合上的纯函数：不读写实例状态，调用方显式传入触点与坐标系

    private static (int Fingers, int Palms) CountTouches(OrderedDictionary<int, TouchContact> contacts)
    {
        var palms = 0;
        foreach (var contact in contacts.Values)
        {
            if (contact.IsPalm)
            {
                palms++;
            }
        }

        return (contacts.Count - palms, palms);
    }

    private static bool TryGetFirstFinger(OrderedDictionary<int, TouchContact> contacts, out TouchContact first)
    {
        foreach (var contact in contacts.Values)
        {
            if (!contact.IsPalm)
            {
                first = contact;
                return true;
            }
        }

        first = default;
        return false;
    }

    private static bool IsPalmTouch(OrderedDictionary<int, TouchContact> contacts, int id)
    {
        return contacts.TryGetValue(id, out var contact) && contact.IsPalm;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double Get1stFingerDispl2(OrderedDictionary<int, TouchContact> contacts, Dictionary<int, Point> starts)
    {
        foreach (var (id, contact) in contacts)
        {
            if (contact.IsPalm)
            {
                continue;
            }

            if (!starts.TryGetValue(id, out var start))
            {
                return 0;
            }

            return Geometry.Distance2(contact.Position, start);
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static (Point? First, Point? Second) GetMajorTouches(OrderedDictionary<int, TouchContact> contacts)
    {
        Point? first = null;
        Point? second = null;

        foreach (var contact in contacts.Values)
        {
            if (contact.IsPalm)
            {
                continue;
            }

            if (first is null)
            {
                first = contact.Position;
                continue;
            }

            second = contact.Position;
            break;
        }

        return (first, second);
    }

    private static bool IsFirstTouch(OrderedDictionary<int, TouchContact> contacts, int id)
    {
        foreach (var (key, contact) in contacts)
        {
            if (contact.IsPalm)
            {
                continue;
            }

            return key == id;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double GetMaxDistance2(OrderedDictionary<int, TouchContact> contacts, int fingerCount)
    {
        if (fingerCount < 2)
        {
            return 0;
        }

        Span<Point> positions = stackalloc Point[fingerCount];
        var index = 0;
        foreach (var kv in contacts)
        {
            if (kv.Value.IsPalm)
            {
                continue;
            }

            positions[index++] = kv.Value.Position;
        }

        double max = 0;
        for (var i = 0; i < fingerCount - 1; i++)
        {
            for (var j = i + 1; j < fingerCount; j++)
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
    private static (Point C1, Point C2) GetPinchPoints(OrderedDictionary<int, TouchContact> contacts, InkCanvas innerCanvas)
    {
        var first = default(TouchContact);
        var second = default(TouchContact);
        var found = false;

        foreach (var contact in contacts.Values)
        {
            if (contact.IsPalm)
            {
                continue;
            }

            if (!found)
            {
                first = contact;
                found = true;
                continue;
            }

            second = contact;
            break;
        }

        return (first.Device.GetTouchPoint(innerCanvas).Position, second.Device.GetTouchPoint(innerCanvas).Position);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Point GetFirstCanvasPoint(OrderedDictionary<int, TouchContact> contacts, InkCanvas innerCanvas)
    {
        foreach (var contact in contacts.Values)
        {
            if (contact.IsPalm)
            {
                continue;
            }

            return contact.Device.GetTouchPoint(innerCanvas).Position;
        }

        return default;
    }

    /// <summary>
    /// 手指触点包络圆：视口坐标下的质心与最远距离，只统计前 limit 个手指触点
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (Point Center, double Radius) GetEnvelopeCircle(OrderedDictionary<int, TouchContact> contacts, int limit)
    {
        var capacity = Math.Min(contacts.Count, limit);
        Span<Point> positions = stackalloc Point[capacity];
        var count = 0;

        foreach (var kv in contacts)
        {
            if (kv.Value.IsPalm)
            {
                continue;
            }

            if (count == capacity)
            {
                break;
            }

            positions[count++] = kv.Value.Position;
        }

        if (count == 0)
        {
            return (default, 1);
        }

        double sumX = 0, sumY = 0;
        foreach (var pos in positions[..count])
        {
            sumX += pos.X;
            sumY += pos.Y;
        }

        var center = new Point(sumX / count, sumY / count);

        double max = 0;
        foreach (var pos in positions[..count])
        {
            var distance2 = Geometry.Distance2(pos, center);
            if (distance2 > max)
            {
                max = distance2;
            }
        }

        return (center, Math.Sqrt(max));
    }

    /// <summary>
    /// 手指触点质心，内容坐标，只统计前 limit 个手指触点
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Point GetCanvasCenter(OrderedDictionary<int, TouchContact> contacts, int limit, InkCanvas innerCanvas)
    {
        var capacity = Math.Min(contacts.Count, limit);
        double sumX = 0, sumY = 0;
        var count = 0;

        foreach (var kv in contacts)
        {
            if (kv.Value.IsPalm)
            {
                continue;
            }

            if (count == capacity)
            {
                break;
            }

            var pos = kv.Value.Device.GetTouchPoint(innerCanvas).Position;
            sumX += pos.X;
            sumY += pos.Y;
            count++;
        }

        return count == 0 ? default : new Point(sumX / count, sumY / count);
    }
}
