using System;
using System.Windows;
using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>滚轮修饰键的组合结果与视图滚动</summary>
public class WheelTests
{
    [Theory]
    [InlineData((int) MouseWheelAction.Scroll, (int) ModifierKeys.None, (int) WheelCommand.Vertical)]
    [InlineData((int) MouseWheelAction.Scroll, (int) ModifierKeys.Control, (int) WheelCommand.Zoom)]
    [InlineData((int) MouseWheelAction.Scroll, (int) ModifierKeys.Alt, (int) WheelCommand.Horizontal)]
    [InlineData((int) MouseWheelAction.Scroll, (int) (ModifierKeys.Control | ModifierKeys.Alt), (int) WheelCommand.Zoom)]
    [InlineData((int) MouseWheelAction.Zoom, (int) ModifierKeys.None, (int) WheelCommand.Zoom)]
    [InlineData((int) MouseWheelAction.Zoom, (int) ModifierKeys.Control, (int) WheelCommand.Zoom)]
    [InlineData((int) MouseWheelAction.Zoom, (int) ModifierKeys.Alt, (int) WheelCommand.Horizontal)]
    [InlineData((int) MouseWheelAction.Zoom, (int) (ModifierKeys.Control | ModifierKeys.Alt), (int) WheelCommand.Zoom)]
    [InlineData((int) MouseWheelAction.None, (int) ModifierKeys.None, (int) WheelCommand.None)]
    [InlineData((int) MouseWheelAction.None, (int) ModifierKeys.Control, (int) WheelCommand.None)]
    [InlineData((int) MouseWheelAction.None, (int) ModifierKeys.Alt, (int) WheelCommand.None)]
    [InlineData((int) MouseWheelAction.None, (int) (ModifierKeys.Control | ModifierKeys.Alt), (int) WheelCommand.None)]
    public void ResolveWheel_ReturnsExpectedCommand(int action, int modifiers, int expected)
    {
        var command = InkCanvasNext.ResolveWheel((MouseWheelAction) action, (ModifierKeys) modifiers);

        Assert.Equal((WheelCommand) expected, command);
    }

    /// <summary>普通滚轮按格数纵向滚动，一格为 ScrollStep 乘缩放</summary>
    [Fact]
    public void WheelAction_Scroll_ScrollsVertically( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var before = host.Canvas.OffsetY;
            var step = InkCanvasNext.ScrollStep * host.Canvas.CurrentScale;

            RaiseWheel(host, (int) InkCanvasNext.WheelDelta);

            Assert.Equal(before - step, host.Canvas.OffsetY, 6);

            RaiseWheel(host, (int) (2 * InkCanvasNext.WheelDelta));

            Assert.Equal(before - 3 * step, host.Canvas.OffsetY, 6);
        });
    }

    /// <summary>关闭滚轮时不接管，偏移不变</summary>
    [Fact]
    public void WheelAction_None_DoesNotScroll( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost(options: new( ) { WheelAction = MouseWheelAction.None });
            var before = host.Canvas.CurrentView;

            RaiseWheel(host, (int) InkCanvasNext.WheelDelta);

            Assert.Equal(before, host.Canvas.CurrentView);
        });
    }

    private static void RaiseWheel(TouchHost host, int delta)
    {
        var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent
        };

        host.Target.RaiseEvent(args);
    }
}
