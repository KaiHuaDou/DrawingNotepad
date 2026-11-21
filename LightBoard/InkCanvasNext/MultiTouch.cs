using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    private readonly Dictionary<int, StrokeVisual> multiTouchStrokes = [];
    private readonly VisualCanvas multiTouchCanvas = new( );

    internal bool AwaitsMultiTouchFrame { get; private set; }

    internal int MultiTouchVisualCount => multiTouchCanvas.VisualCount;

    private void StartMultiTouchStroke(int touchId, Point canvasPoint)
    {
        // 盖章不落多指笔画；擦除工具与只读模式不进入多指书写
        if (Snapshot.IsStamp || !Snapshot.AllowsMultiDraw)
        {
            return;
        }

        var attributes = InnerCanvas.DefaultDrawingAttributes.Clone( );
        var initialPoint = new StylusPoint(canvasPoint.X, canvasPoint.Y, DefaultPressure);
        var strokeVisual = new StrokeVisual(attributes, initialPoint, Taper.Effective(options.Taper, attributes));
        strokeVisual.VisualCanvas = multiTouchCanvas;

        multiTouchStrokes[touchId] = strokeVisual;
        strokeVisual.Redraw( );
    }

    /// <summary>
    /// 迁入 MultiDraw 前保住原生在绘笔画
    /// InkCanvas 切换编辑模式丢弃在绘笔画
    /// </summary>
    private void HandOffDrawingStroke( )
    {
        if (State is TouchState.EvalDraw or TouchState.Draw
            && InnerCanvas.EditingMode == InkCanvasEditingMode.Ink
            && InnerCanvas.IsStylusCaptured)
        {
            InnerCanvas.ReleaseStylusCapture( );
        }
    }

    private void StartMultiTouch( )
    {
        foreach (var (id, (device, _)) in touches)
        {
            if (multiTouchStrokes.ContainsKey(id))
            {
                continue;
            }

            StartMultiTouchStroke(id, device.GetTouchPoint(InnerCanvas).Position);
        }
    }

    private void ContinueMultiTouchStroke(int touchId, Point canvasPoint)
    {
        if (!multiTouchStrokes.TryGetValue(touchId, out var strokeVisual))
        {
            return;
        }

        strokeVisual.Add(new StylusPoint(canvasPoint.X, canvasPoint.Y, DefaultPressure));
        HookMultiTouchFrame( );
    }

    private void EndMultiTouchStroke(int touchId)
    {
        if (!multiTouchStrokes.TryGetValue(touchId, out var strokeVisual))
        {
            return;
        }

        var stroke = strokeVisual.BuildStroke( );
        if (stroke is not null)
        {
            InnerCanvas.Strokes.Add(stroke);
        }

        strokeVisual.Cleanup( );

        multiTouchStrokes.Remove(touchId);

        if (multiTouchStrokes.Count == 0)
        {
            UnhookMultiTouchFrame( );
        }
    }

    private void EndMultiTouch( )
    {
        foreach (var id in multiTouchStrokes.Keys.ToArray( ))
        {
            EndMultiTouchStroke(id);
        }
    }

    /// <summary>
    /// 清空所有进行中的多指笔画视觉
    /// </summary>
    public void ClearMultiTouchVisuals( )
    {
        foreach (var strokeVisual in multiTouchStrokes.Values)
        {
            strokeVisual.Cleanup( );
        }

        multiTouchStrokes.Clear( );
        UnhookMultiTouchFrame( );
    }

    private void HookMultiTouchFrame( )
    {
        if (AwaitsMultiTouchFrame)
        {
            return;
        }

        AwaitsMultiTouchFrame = true;
        CompositionTarget.Rendering += MultiTouchFrame;
    }

    private void UnhookMultiTouchFrame( )
    {
        if (!AwaitsMultiTouchFrame)
        {
            return;
        }

        AwaitsMultiTouchFrame = false;
        CompositionTarget.Rendering -= MultiTouchFrame;
    }

    /// <summary>
    /// 帧节拍：一帧内到达的触点合并成一次几何重建
    /// </summary>
    private void MultiTouchFrame(object? o, EventArgs e)
    {
        var redrawn = false;

        foreach (var strokeVisual in multiTouchStrokes.Values)
        {
            if (!strokeVisual.NeedsRedraw)
            {
                continue;
            }

            strokeVisual.Redraw( );
            redrawn = true;
        }

        if (!redrawn)
        {
            UnhookMultiTouchFrame( );
        }
    }
}
