using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>
/// 湿墨轮廓：活动段包络与尾部收细；几何坐标与宽度都按 DrawingAttributes 的压力映射
/// </summary>
public class TaperOutlineTests
{
    private static readonly TaperParameters Taper = new( ) { Points = 8 };

    private static List<StylusPoint> Line(int count)
    {
        var points = new List<StylusPoint>(count);
        for (var i = 0; i < count; i++)
        {
            points.Add(new StylusPoint(i * 10, 0, 0.5f));
        }

        return points;
    }

    [Fact]
    public void SinglePoint_UsesFullWidth( )
    {
        StaTest.Run(( ) =>
        {
            var points = Line(1);

            var geometry = TaperOutline.Build(points, 0, 1, 10, Taper, 1);

            Assert.NotNull(geometry);
            Assert.InRange(geometry!.Bounds.Width, 9.99, 10.01);
            Assert.InRange(geometry.Bounds.Height, 9.99, 10.01);
        });
    }

    [Fact]
    public void TailPoint_UsesTipRatio( )
    {
        StaTest.Run(( ) =>
        {
            var points = Line(4);

            // 4 点、尾长 3：最后一点的 k = 2，Ratio = 0.25
            var geometry = TaperOutline.Build(points, 3, 4, 10, Taper, 1);

            Assert.NotNull(geometry);
            Assert.InRange(geometry!.Bounds.Width, 2.49, 2.51);
            Assert.InRange(geometry.Bounds.Height, 2.49, 2.51);
        });
    }

    [Fact]
    public void BodySegment_KeepsFullWidth( )
    {
        StaTest.Run(( ) =>
        {
            var points = Line(8);

            // 笔身区间全部在尾部之前，宽度不收缩
            var geometry = TaperOutline.Build(points, 0, 5, 10, Taper, 8);

            Assert.NotNull(geometry);
            Assert.InRange(geometry!.Bounds.Height, 9.99, 10.01);
        });
    }

    [Fact]
    public void EmptyRange_ReturnsNull( )
    {
        StaTest.Run(( ) =>
        {
            var points = Line(3);

            Assert.Null(TaperOutline.Build(points, 2, 2, 10, Taper, 1));
            Assert.Null(TaperOutline.Build(points, 0, 4, 10, Taper, 1));
            Assert.Null(TaperOutline.Build(points, 0, 2, 0, Taper, 1));
        });
    }

    [Fact]
    public void Outline_FillsCapCenters( )
    {
        StaTest.Run(( ) =>
        {
            var points = Line(4);

            var geometry = TaperOutline.Build(points, 0, 4, 10, Taper, 3);

            Assert.NotNull(geometry);

            // 端帽必须与包络同向填充；反向时重叠区绕数为 0，会挖出圆洞，肉眼看笔迹末端分叉
            Assert.True(geometry!.FillContains(new Point(points[0].X, points[0].Y)), "起点端帽中心是空的");
            Assert.True(geometry.FillContains(new Point(points[^1].X, points[^1].Y)), "末端端帽中心是空的");
            Assert.True(geometry.FillContains(new Point(15, 0.5)), "笔身中心是空的");
        });
    }

    [Fact]
    public void Outline_ContainsSampledCenterLine( )
    {
        StaTest.Run(( ) =>
        {
            var points = Line(4);

            var geometry = TaperOutline.Build(points, 0, 4, 10, Taper, 3);

            Assert.NotNull(geometry);
            foreach (var point in points)
            {
                Assert.True(geometry!.Bounds.Contains(new Point(point.X, point.Y)));
            }
        });
    }
}
