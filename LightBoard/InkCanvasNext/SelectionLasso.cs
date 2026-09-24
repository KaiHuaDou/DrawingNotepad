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
    private bool lassoDragged;

    // ---------- 套索 ----------

    /// <summary>空白处按下（鼠标/触摸共用）：清空当前选型，以落点为起点开始增量套索测试。</summary>
    private void BeginLasso(Point p)
    {
        selection.Clear( );
        selectionGesture = SelectionGesture.Lasso;
        lassoPath.Add(p);
        lassoDragged = false;
        lassoTester = InnerCanvas.Strokes.GetIncrementalLassoHitTester(50);
        lassoTester.SelectionChanged += OnLassoSelectionChanged;
        lassoTester.AddPoints([ToStylusPoint(p)]);
    }

    /// <summary>套索轨迹追加（鼠标/触摸共用）：去抖后累积路径点并更新命中测试与套索视觉。</summary>
    private void UpdateLassoTrack(Point p)
    {
        if (Distance2(p, lassoPath[^1]) < LassoPointDistance2)
        {
            return;
        }

        lassoDragged = true;
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

        lassoPath.Clear( );
        selection.InvalidateLasso(null);

        if (lassoDragged)
        {
            selection.SetStrokes(lassoSelected);
        }
        else
        {
            // 无拖拽视为点选：命中单笔则选中，否则清除
            HandlePointSelect(selectionStartPoint);
        }
    }

    private void HandlePointSelect(Point p)
    {
        var hit = InnerCanvas.Strokes.HitTest(p);
        if (hit.Count > 0)
        {
            selection.SetStrokes(hit);
        }
        else
        {
            selection.Clear( );
        }
    }
}
