using System.Reflection;
using System.Windows.Ink;

namespace LightBoard.Tests.Toolbar;

internal static class TestSelection
{
    // 内部 SelectionController 没有公开入口，测试直接经反射注入选区，替代现实的套索手势。
    public static void ForceSelection(InkCanvasNext.InkCanvasNext canvas, StrokeCollection strokes)
    {
        var controller = typeof(InkCanvasNext.InkCanvasNext)
            .GetField("selection", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(canvas)!;
        var setStrokes = controller
            .GetType( )
            .GetMethod("SetStrokes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        setStrokes.Invoke(controller, [strokes]);
    }
}