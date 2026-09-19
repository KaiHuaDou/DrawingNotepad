using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 工具条测试宿主：在 App 的 STA 线程上构造真实 MainWindow（不显示），
/// 提供控件查找、事件驱动与状态断言入口。
/// </summary>
public sealed class MainWindowHost : IDisposable
{
    private readonly EventHandler pageHandler;

    public MainWindow Window { get; }
    public InkCanvasNext.InkCanvasNext Canvas => Window.CanvasNext;

    public MainWindowHost( )
    {
        App.Pages.Clear( );
        Window = new MainWindow( );
        pageHandler = Window.OnPageChanged;
    }

    public void Dispose( )
    {
        App.PageChanged -= pageHandler;
        App.Pages.Clear( );
        Window.Close( );
    }

    // ---- 控件查找 ----

    public IReadOnlyList<RadioButton> ColorRadios => RadiosInGroup("ColorGroup");

    public IReadOnlyList<RadioButton> ThicknessRadios => RadiosInGroup("ThicknessGroup");

    public IReadOnlyList<RadioButton> RadiosInGroup(string group)
    {
        return [.. Descendants((DependencyObject) Window.Content)
            .OfType<RadioButton>( )
            .Where(r => r.GroupName == group)];
    }

    public RadioButton FindColor(Color color)
    {
        return ColorRadios.Single(r => ((SolidColorBrush) r.Background).Color == color);
    }

    public RadioButton FindThickness(double width)
    {
        return ThicknessRadios.Single(r => r.MinWidth == width);
    }

    public Button FindButtonByTag(string tag)
    {
        return Descendants((DependencyObject) Window.Content)
            .OfType<Button>( )
            .Single(b => (b.Tag as string) == tag);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>( );
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject d)
            {
                queue.Enqueue(d);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue( );
            yield return current;
            foreach (var child in LogicalTreeHelper.GetChildren(current))
            {
                if (child is DependencyObject cd)
                {
                    queue.Enqueue(cd);
                }
            }
        }
    }
}
