using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

internal static class TestStrokes
{
    public static Stroke MakeStroke(Color? color = null, double width = 3)
    {
        return new Stroke(
            [new StylusPoint(0, 0), new StylusPoint(10, 10)],
            new DrawingAttributes { Color = color ?? Colors.Black, Width = width, Height = width });
    }
}