using System.Windows.Controls;

namespace InkCanvasNext;

internal enum TouchState
{
    Idle,
    EvalDraw,
    Draw,
    PanZoom,
    Pan,
    Eraser,
    MultiDraw
}

public partial class InkCanvasNext
{
    private bool UpdateState( )
    {
        var count = touches.Count;
        var d2 = GetMaxDistance2( );
        var l2 = distanceThreshold2;
        var x2 = Get1stFingerDispl2( );
        var c2 = touchDisplThreshold * touchDisplThreshold;

        TouchState newState = currentState switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.EvalDraw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.MultiDraw => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                _ => currentState,
            },

            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 2 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 3 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                > 5 when d2 > l2 => TouchState.MultiDraw,
                _ => currentState,
            },

            _ => currentState,
        };

        SetState(newState);
        return newState is TouchState.PanZoom or TouchState.Pan;
    }

    private void SetState(TouchState newState)
    {
        if (currentState == newState)
        {
            return;
        }

        switch ((currentState, newState))
        {
            case (TouchState.Idle, TouchState.EvalDraw):
                baseEditingMode = Mode;
                break;

            case (TouchState.Idle, TouchState.PanZoom):
            case (TouchState.Idle, TouchState.Pan):
                baseEditingMode = Mode;
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                InitGesture( );
                break;

            case (TouchState.Idle, TouchState.Eraser):
                baseEditingMode = Mode;
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.Idle, TouchState.MultiDraw):
                baseEditingMode = Mode;
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.EvalDraw, TouchState.Idle):
            case (TouchState.Draw, TouchState.Idle):
            case (TouchState.MultiDraw, TouchState.Idle):
            case (TouchState.PanZoom, TouchState.Idle):
            case (TouchState.Pan, TouchState.Idle):
            case (TouchState.Eraser, TouchState.Idle):
                ReleaseAll( );
                RestoreEditingMode( );
                break;

            case (TouchState.EvalDraw, TouchState.Draw):
                break;

            case (TouchState.EvalDraw, TouchState.PanZoom):
            case (TouchState.EvalDraw, TouchState.Pan):
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                InitGesture( );
                break;

            case (TouchState.MultiDraw, TouchState.Draw):
                RestoreEditingMode( );
                break;

            case (TouchState.PanZoom, TouchState.Pan):
                InitGesture( );
                break;

            case (TouchState.EvalDraw, TouchState.Eraser):
            case (TouchState.PanZoom, TouchState.Eraser):
            case (TouchState.Pan, TouchState.Eraser):
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                break;

            case (TouchState.EvalDraw, TouchState.MultiDraw):
            case (TouchState.Draw, TouchState.MultiDraw):
            case (TouchState.PanZoom, TouchState.MultiDraw):
            case (TouchState.Pan, TouchState.MultiDraw):
            case (TouchState.Eraser, TouchState.MultiDraw):
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            default:
                break;
        }

        currentState = newState;
    }

    private void RestoreEditingMode( )
    {
        switch (baseEditingMode)
        {
            case InkCanvasNextMode.Ink:
            case InkCanvasNextMode.EraseArea:
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;
            case InkCanvasNextMode.EraseStroke:
                Canvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                break;
            case InkCanvasNextMode.Select:
                Canvas.EditingMode = InkCanvasEditingMode.Select;
                break;
        }
    }
}
