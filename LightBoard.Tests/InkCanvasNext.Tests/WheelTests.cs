using System.Windows.Input;

namespace InkCanvasNext.Tests;

/// <summary>滚轮修饰键的组合结果。</summary>
public class WheelTests
{
    [Theory]
    [InlineData((int) MouseWheelAction.Scroll, (int) ModifierKeys.None, (int) WheelCommand.None)]
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
}
