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

    /// <summary>按选项分发空白处起手：矩形框选或套索圈选</summary>
    private void BeginSelectionDrag(Point p)
    {
        if (options.SelectionDragMode == SelectionDragMode.Rectangle)
        {
            BeginRectangle(p);
        }
        else
        {
            BeginLasso(p);
        }
    }

    /// <summary>空白处按下（鼠标/触摸共用）：以按下点为起点开始增量套索测试，原选型留到路径真正拉开时再清</summary>
    private void BeginLasso(Point p)
    {
        // 路径与命中集合都按本次套索重新累积：跨次残留会让第二次套索带上上一次的笔画
        lassoPath.Clear( );
        lassoSelected.Clear( );
        selectionGesture = SelectionGesture.Lasso;
        lassoPath.Add(p);
        lassoTester = InnerCanvas.Strokes.GetIncrementalLassoHitTester(LassoHitPercentage);
        lassoTester.SelectionChanged += OnLassoSelectionChanged;
        lassoTester.AddPoints([ToStylusPoint(p)]);
    }

    /// <summary>套索轨迹追加（鼠标/触摸共用）：去抖后累积路径点并更新命中测试与套索视觉</summary>
    private void UpdateLassoTrack(Point p)
    {
        // 采样间距为视口 DIP：平方距离比较除以缩放平方转内容，保持屏幕绝对大小
        var minDistance2 = LassoPointDistance2 / (CurrentScale * CurrentScale);
        if (Distance2(p, lassoPath[^1]) < minDistance2)
        {
            return;
        }

        // 起手不清选型，第二根手指落在选区内时可以切回拖动；路径已经拉开才算新的一次选择
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
            // 无拖拽视为点选：命中单笔则选中，否则清除
            HandlePointSelect(selectionStartPoint, halo: false);
        }
    }

    private void HandlePointSelect(Point p, bool halo = true)
    {
        var hit = InnerCanvas.Strokes.HitTest(p);
        if (hit.Count > 0)
        {
            selection.SetStrokes(hit, halo);
        }
        else
        {
            selection.Clear( );
        }
    }
}
