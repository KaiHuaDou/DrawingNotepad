using System.Windows;

using InkCanvasNext;

namespace LightBoard;

public partial class MainWindow
{
    private void UpdateEdgeHintButtons( )
    {
        TopEdgeHint.Visibility = EdgeHintVisibility(EdgeScrollSide.Top);
        BottomEdgeHint.Visibility = EdgeHintVisibility(EdgeScrollSide.Bottom);
        LeftEdgeHint.Visibility = EdgeHintVisibility(EdgeScrollSide.Left);
        RightEdgeHint.Visibility = EdgeHintVisibility(EdgeScrollSide.Right);
    }

    private Visibility EdgeHintVisibility(EdgeScrollSide side)
    {
        return CanvasNext.IsEdgeHintVisible(side) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateEdgeHintMargin( )
    {
        BottomEdgeHint.Margin = new Thickness(0, 0, 0, EdgeHintBottomMargin( ));
    }

    private void TopEdgeHintClick(object o, RoutedEventArgs e)
    {
        CanvasNext.ScrollEdgeClear(EdgeScrollSide.Top);
    }

    private void BottomEdgeHintClick(object o, RoutedEventArgs e)
    {
        CanvasNext.ScrollEdgeClear(EdgeScrollSide.Bottom);
    }

    private void LeftEdgeHintClick(object o, RoutedEventArgs e)
    {
        CanvasNext.ScrollEdgeClear(EdgeScrollSide.Left);
    }

    private void RightEdgeHintClick(object o, RoutedEventArgs e)
    {
        CanvasNext.ScrollEdgeClear(EdgeScrollSide.Right);
    }
}
