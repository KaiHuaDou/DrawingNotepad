using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 湿墨自绘层：点数与视觉数量、设备触点描述、分段冻结上限、荧光笔整笔重绘
/// </summary>
public class WetInkTests
{
    private static readonly Point Start = new(100, 100);

    // 笔设备上报的触点描述带 PacketStatus 等设备属性
    private static readonly StylusPointDescription DeviceDescription = new(
    [
        new StylusPointPropertyInfo(StylusPointProperties.X),
        new StylusPointPropertyInfo(StylusPointProperties.Y),
        new StylusPointPropertyInfo(StylusPointProperties.NormalPressure),
        new StylusPointPropertyInfo(StylusPointProperties.PacketStatus)
    ]);

    private static StylusPointCollection PointsAt(params Point[] positions)
    {
        var points = new StylusPointCollection(positions.Length);
        foreach (var position in positions)
        {
            points.Add(new StylusPoint(position.X, position.Y));
        }

        return points;
    }

    private static StylusPointCollection DevicePoints(params Point[] positions)
    {
        var points = new StylusPointCollection(DeviceDescription, positions.Length);
        foreach (var position in positions)
        {
            points.Add(new StylusPoint(position.X, position.Y, 0.5f, DeviceDescription, [0]));
        }

        return points;
    }

    private static void Drag(WetInk wet, int count)
    {
        for (var i = 1; i < count; i++)
        {
            wet.Add(1, PointsAt(new Point(Start.X + i, Start.Y)));
        }
    }

    [Fact]
    public void BeginAddClear_TracksPointsAndVisuals( )
    {
        StaTest.Run(( ) =>
        {
            var wet = new WetInk(new TaperParameters { Points = 8 });
            var attributes = new DrawingAttributes
            {
                Width = 6,
                Height = 6,
                Color = Colors.White
            };

            wet.Begin(1, attributes, PointsAt(Start));
            Assert.Equal(1, wet.PointCount);
            Assert.Equal(1, wet.VisualCount);

            wet.Add(1, PointsAt(new Point(200, 100), new Point(300, 100)));
            Assert.Equal(3, wet.PointCount);

            wet.Add(2, PointsAt(new Point(400, 100)));
            Assert.Equal(3, wet.PointCount);

            wet.Clear(1);
            Assert.Equal(0, wet.PointCount);
            Assert.Equal(0, wet.VisualCount);
        });
    }

    [Fact]
    public void LongStroke_FreezesSegmentsBeforeTail( )
    {
        StaTest.Run(( ) =>
        {
            var wet = new WetInk(new TaperParameters { Points = 8 });
            var attributes = new DrawingAttributes
            {
                Width = 6,
                Height = 6,
                Color = Colors.White
            };

            wet.Begin(1, attributes, PointsAt(Start));
            Drag(wet, 200);

            Assert.Equal(200, wet.PointCount);
            Assert.True(wet.VisualCount >= 2);

            wet.Abort( );
            Assert.Equal(0, wet.PointCount);
            Assert.Equal(0, wet.VisualCount);
        });
    }

    [Fact]
    public void Begin_AcceptsDevicePointDescription( )
    {
        StaTest.Run(( ) =>
        {
            var wet = new WetInk(new TaperParameters { Points = 8 });
            var attributes = new DrawingAttributes
            {
                Width = 6,
                Height = 6,
                Color = Colors.White
            };

            wet.Begin(1, attributes, DevicePoints(Start));
            Assert.Equal(1, wet.PointCount);
            Assert.Equal(1, wet.VisualCount);

            wet.Add(1, DevicePoints(new Point(200, 100)));
            Assert.Equal(2, wet.PointCount);

            wet.Abort( );
        });
    }

    [Fact]
    public void Highlighter_KeepsSingleVisual( )
    {
        StaTest.Run(( ) =>
        {
            var wet = new WetInk(new TaperParameters { Points = 8 });
            var attributes = new DrawingAttributes
            {
                Width = 30,
                Height = 30,
                IsHighlighter = true,
                Color = Colors.Yellow
            };

            wet.Begin(1, attributes, PointsAt(Start));
            Drag(wet, 200);

            // 半透明笔迹不能拆段：段间共享的接头点会叠色
            Assert.Equal(1, wet.VisualCount);

            wet.Abort( );
        });
    }
}
