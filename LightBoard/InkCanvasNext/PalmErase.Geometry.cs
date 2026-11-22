using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;

namespace InkCanvasNext;

/// <summary>
/// 手掌几何：视口质心、内容质心、包围盒、分档直径、簇成员签名
/// </summary>
internal readonly record struct PalmGeometry(
    Point Center,
    Point CanvasCenter,
    double Width,
    double Height,
    double Diagonal,
    double AreaDiameter,
    double EnvelopeDiameter,
    double RawDiameter,
    int ClusterSignature);

public partial class InkCanvasNext
{
    /// <summary>
    /// 手掌簇的包围盒，尺寸参与边界计算
    /// </summary>
    private static (double Width, double Height) GetPalmBox(List<TouchContact> contacts)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var contact in contacts)
        {
            var halfWidth = contact.Size.Width / 2;
            var halfHeight = contact.Size.Height / 2;
            minX = Math.Min(minX, contact.Position.X - halfWidth);
            maxX = Math.Max(maxX, contact.Position.X + halfWidth);
            minY = Math.Min(minY, contact.Position.Y - halfHeight);
            maxY = Math.Max(maxY, contact.Position.Y + halfHeight);
        }

        return (maxX - minX, maxY - minY);
    }

    /// <summary>
    /// 手掌簇的包络圆：视口坐标下的质心与最远距离
    /// </summary>
    private static (Point Center, double Radius) GetClusterEnvelope(List<TouchContact> contacts)
    {
        double sumX = 0, sumY = 0;
        foreach (var contact in contacts)
        {
            sumX += contact.Position.X;
            sumY += contact.Position.Y;
        }

        var center = new Point(sumX / contacts.Count, sumY / contacts.Count);

        double max = 0;
        foreach (var contact in contacts)
        {
            var distance2 = Geometry.Distance2(contact.Position, center);
            if (distance2 > max)
            {
                max = distance2;
            }
        }

        return (center, Math.Sqrt(max));
    }

    /// <summary>
    /// 手掌簇的成员签名：设备号与成员数决定；成员变化意味着擦除主体换了
    /// </summary>
    private static int GetClusterSignature(List<TouchContact> contacts)
    {
        var hash = unchecked(17 * 31 + contacts.Count);
        foreach (var contact in contacts)
        {
            hash = unchecked(hash * 31 + contact.Device.Id);
        }

        return hash;
    }

    /// <summary>
    /// 接触区域对角线分档：300 以下 1.5，720 以上逐档放大
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double PalmRatio(double diagonal)
    {
        return diagonal switch
        {
            < 300 => 1.5,
            < 720 => 2.2,
            < 1000 => 2.85,
            _ => 3.5,
        };
    }
}
