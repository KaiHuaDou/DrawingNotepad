using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;

using static InkCanvasNext.Geometry;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly List<Point> lassoPath = [];
    private readonly HashSet<Stroke> lassoSelected = [];
    private IncrementalLassoHitTester? lassoTester;

    // 套索

    private void BeginLasso(Point p)
    {
        // 路径与命中集合都从头累积，否则第二次套索会带上上一次的笔画
        lassoPath.Clear( );
        lassoSelected.Clear( );
        selectionGesture = SelectionGesture.Lasso;
        lassoPath.Add(p);
        lassoTester = InnerCanvas.Strokes.GetIncrementalLassoHitTester(LassoHitPercentage);
        lassoTester.SelectionChanged += OnLassoSelectionChanged;
        lassoTester.AddPoints([ToStylusPoint(p)]);
    }

    private void UpdateLassoTrack(Point p)
    {
        // 采样间距为视口 DIP：平方距离比较除以缩放平方转内容，保持屏幕绝对大小
        var minDistance2 = LassoPointDistance2 / (CurrentScale * CurrentScale);
        if (Distance2(p, lassoPath[^1]) < minDistance2)
        {
            return;
        }

        // 起手不清选型，第二根手指落在选区内时可以切回拖动；路径拉开后才算新的一次选择
        selection.Clear( );
        lassoPath.Add(p);
        lassoTester?.AddPoints([ToStylusPoint(p)]);
        selection.InvalidateLasso(lassoPath);
    }

    private void OnLassoSelectionChanged(object? sender, LassoSelectionChangedEventArgs e)
    {
        foreach (var s in e.SelectedStrokes)
        {
            lassoSelected.Add(s);
        }

        foreach (var s in e.DeselectedStrokes)
        {
            lassoSelected.Remove(s);
        }
    }

    private void EndLasso( )
    {
        lassoTester?.EndHitTesting( );
        lassoTester?.SelectionChanged -= OnLassoSelectionChanged;
        lassoTester = null;

        var dragged = lassoPath.Count > 1;
        lassoPath.Clear( );
        selection.InvalidateLasso(null);

        if (dragged)
        {
            selection.SetStrokes(lassoSelected, halo: false);
        }
        else
        {
            HandlePointSelect(selectionStartPoint, halo: false);
        }
    }
}
