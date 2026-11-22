using System.Windows;
using System.Windows.Interop;

using LightBoard;

namespace InkCanvasNext.Tests;

/// <summary>
/// 宿主：在不可见 HwndSource 中装配 InkCanvasNext 并完成布局
/// 触摸事件处理目标为内部墨迹画布（InnerCanvasElement）
/// </summary>
internal sealed class TouchHost : IDisposable
{
    private readonly HwndSource source;
    private readonly List<MockTouchDevice> devices = [];

    public InkCanvasNext Canvas { get; }

    /// <summary>画布累计向左/上平移的内容量，内容坐标</summary>
    public Vector ContentShift { get; private set; }

    public InkCanvasNextMode Mode
    {
        get => Canvas.DrawingAttributes.Mode;
        set => Canvas.DrawingAttributes = Canvas.DrawingAttributes with { Mode = value };
    }

    /// <summary>触摸事件处理目标（内部墨迹画布）</summary>
    public UIElement Target => Canvas.InnerCanvas;

    public TouchHost(double width = 1280, double height = 800, InkCanvasNextOptions? options = null)
    {
        Canvas = new InkCanvasNext(App.CanvasSize, App.InitialOffset, options);
        Canvas.ContentShifted += (_, e) => ContentShift += e.Offset;

        var parameters = new HwndSourceParameters("InkCanvasNext.Tests")
        {
            Width = (int) width,
            Height = (int) height,
            WindowStyle = 0,
            ExtendedWindowStyle = 0
        };

        source = new HwndSource(parameters)
        {
            RootVisual = Canvas
        };

        Canvas.Measure(new Size(width, height));
        Canvas.Arrange(new Rect(0, 0, width, height));
        Canvas.UpdateLayout( );
    }

    public MockTouchDevice Device( )
    {
        var device = new MockTouchDevice( );
        devices.Add(device);
        return device;
    }

    public void Dispose( )
    {
        foreach (var device in devices)
        {
            device.ForceEnd( );
        }

        Canvas.ResetTouchState( );
        source.Dispose( );
    }
}
