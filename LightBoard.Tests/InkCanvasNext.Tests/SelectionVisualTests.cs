using System.Windows;

namespace InkCanvasNext.Tests;

public class SelectionVisualTests
{
    // 旋转手柄恒定悬浮于选区上沿上方，间距 = (RotateGapAboveSelection + RotateScreenRadius) / zoom，zoom 非正时按 1e-6 计
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(0.5)]
    [InlineData(0.0)]
    public void RotateHandleCenter_HoversAboveSelectionTop(double zoom)
    {
        var bounds = new Rect(100, 200, 400, 300);
        var handle = SelectionVisual.RotateHandleCenter(bounds, zoom);

        Assert.Equal(bounds.Left + bounds.Width / 2, handle.X, 12);
        Assert.True(handle.Y < bounds.Top);
        Assert.Equal(40 / Math.Max(zoom, 1e-6), bounds.Top - handle.Y, 12);
    }
}
