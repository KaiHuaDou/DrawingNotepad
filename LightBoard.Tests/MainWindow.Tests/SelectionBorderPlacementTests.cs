using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 选区工具条跟随选区：位置写在渲染变换上，布局用的 Margin 保持为零
/// </summary>
[Collection("Toolbar")]
public class SelectionBorderPlacementTests
{
    [Fact]
    public void Selection_PlacesToolbarByRenderTransform( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            // 未显示的 Window 上 UpdateLayout 不完整，坐标换算需要真实布局
            host.Window.WindowState = WindowState.Normal;
            host.Window.ShowActivated = false;
            host.Window.Show( );
            host.Window.UpdateLayout( );

            ToolEntry.Enter(host, InkCanvasNextMode.Select);

            var canvas = host.Canvas;
            var origin = App.InitialOffset;
            canvas.Strokes.Add(new Stroke(
                [new StylusPoint(origin.X + 100, origin.Y + 100), new StylusPoint(origin.X + 200, origin.Y + 160)],
                new DrawingAttributes { Color = Colors.Black, Width = 3, Height = 3 }));

            host.Window.UpdateLayout( );
            TestSelection.ForceSelection(canvas, canvas.Strokes);
            host.Window.UpdateLayout( );

            var border = host.Window.SelectionBorder;
            Assert.Equal(Visibility.Visible, border.Visibility);
            Assert.Equal(default, border.Margin);

            var offset = Assert.IsType<TranslateTransform>(border.RenderTransform);
            var bounds = canvas.GetSelectionScreenBounds((UIElement) border.Parent);
            Assert.NotNull(bounds);

            // 下方放得下：工具条吸附到选区可见部分左下角
            Assert.Equal(bounds.Value.Left, offset.X, 1);
            Assert.Equal(bounds.Value.Bottom + InkCanvasNext.InkCanvasNext.SelectionToolbarGap, offset.Y, 1);
        });
    }
}
