using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext.Tests;

/// <summary>
/// 自绘笔画：点数与视觉数量、设备触点描述、分段冻结上限、荧光笔整笔重绘
/// </summary>
public class StrokeVisualTests
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

    private static StrokeVisual Create(DrawingAttributes attributes, TaperParameters? taper = null)
    {
        return new StrokeVisual(attributes, new StylusPoint(Start.X, Start.Y), taper);
    }

    private static DrawingAttributes Pen( )
    {
        return new DrawingAttributes
        {
            Width = 6,
            Height = 6,
            Color = Colors.White
        };
    }

    private static int PointCount(StrokeVisual visual)
    {
        return visual.Stroke.StylusPoints.Count;
    }

    [Fact]
    public void AddRedrawCleanup_TracksPointsAndVisuals( )
    {
        StaTest.Run(( ) =>
        {
            var layer = new VisualCanvas( );
            var visual = Create(Pen( ), new TaperParameters { Points = 8 });
            visual.SetVisualCanvas(layer);

            visual.Redraw( );
            Assert.Equal(1, PointCount(visual));
            Assert.Equal(1, layer.VisualCount);

            visual.Add(new StylusPoint(200, 100));
            visual.Add(new StylusPoint(300, 100));
            visual.Redraw( );
            Assert.Equal(3, PointCount(visual));

            visual.Cleanup( );
            Assert.Equal(0, layer.VisualCount);
        });
    }

    [Fact]
    public void LongStroke_FreezesSegmentsBeforeTail( )
    {
        StaTest.Run(( ) =>
        {
            var layer = new VisualCanvas( );
            var visual = Create(Pen( ), new TaperParameters { Points = 8 });
            visual.SetVisualCanvas(layer);

            visual.Redraw( );
            for (var i = 1; i < 200; i++)
            {
                visual.Add(new StylusPoint(Start.X + i, Start.Y));
            }

            Assert.Equal(200, PointCount(visual));

            // 64 点一冻结：200 点至少留下两个冻结段，加上活动段
            Assert.True(layer.VisualCount >= 2, $"视觉数不足：{layer.VisualCount}");

            visual.Cleanup( );
            Assert.Equal(0, layer.VisualCount);
        });
    }

    [Fact]
    public void Begin_AcceptsDevicePointDescription( )
    {
        StaTest.Run(( ) =>
        {
            var initial = new StylusPoint(Start.X, Start.Y, 0.5f, DeviceDescription, [0]);
            var visual = new StrokeVisual(Pen( ), initial, null);
            visual.SetVisualCanvas(new VisualCanvas( ));
            visual.Add(new StylusPoint(200, 100, 0.5f, DeviceDescription, [0]));

            Assert.Equal(2, PointCount(visual));
            Assert.Same(DeviceDescription, visual.Stroke.StylusPoints.Description);

            visual.Cleanup( );
        });
    }

    [Fact]
    public void Highlighter_KeepsSingleVisual( )
    {
        StaTest.Run(( ) =>
        {
            var layer = new VisualCanvas( );
            var attributes = Pen( );
            attributes.IsHighlighter = true;
            attributes.Color = Colors.Yellow;

            var visual = Create(attributes, new TaperParameters { Points = 8 });
            visual.SetVisualCanvas(layer);
            visual.Redraw( );

            for (var i = 1; i < 200; i++)
            {
                visual.Add(new StylusPoint(Start.X + i, Start.Y));
            }

            // 半透明笔迹不能拆段：段间共享的接头点会叠色
            Assert.Equal(1, layer.VisualCount);

            visual.Cleanup( );
        });
    }
}
