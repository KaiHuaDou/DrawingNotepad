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

        var newState = state switch
        {
            TouchState.Idle => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.EvalDraw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.EvalDraw => count switch
            {
                0 => TouchState.Idle,
                1 when x2 > c2 => TouchState.Draw,
                2 when d2 <= l2 => TouchState.PanZoom,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Draw => count switch
            {
                0 => TouchState.Idle,
                >= 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.MultiDraw => count switch
            {
                0 => TouchState.Idle,
                1 => TouchState.Draw,
                _ => state,
            },

            TouchState.PanZoom => count switch
            {
                0 => TouchState.Idle,
                3 or 4 when d2 <= l2 => TouchState.Pan,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 2 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Pan => count switch
            {
                0 => TouchState.Idle,
                >= 5 when d2 <= l2 => TouchState.Eraser,
                > 3 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            TouchState.Eraser => count switch
            {
                0 => TouchState.Idle,
                > 5 when d2 > l2 => TouchState.MultiDraw,
                _ => state,
            },

            _ => state,
        };

        SetState(newState);
        return newState is TouchState.PanZoom or TouchState.Pan;
    }

    private void SetState(TouchState newState)
    {
        if (state == newState)
        {
            return;
        }

        switch ((state, newState))
        {
            case (TouchState.Idle, TouchState.EvalDraw):
                prevMode = Mode;
                if (Mode == InkCanvasNextMode.EraseArea)
                {
                    CaptureAll( );
                }

                break;

            case (TouchState.Idle, TouchState.PanZoom):
            case (TouchState.Idle, TouchState.Pan):
                prevMode = Mode;
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                InitGesture( );
                break;

            case (TouchState.Idle, TouchState.Eraser):
                prevMode = Mode;
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                break;

            case (TouchState.Idle, TouchState.MultiDraw):
                prevMode = Mode;
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;

            case (TouchState.EvalDraw, TouchState.Idle):
            case (TouchState.Draw, TouchState.Idle):
                ReleaseAll( );
                RestoreMode( );
                break;

            case (TouchState.MultiDraw, TouchState.Idle):
            case (TouchState.PanZoom, TouchState.Idle):
            case (TouchState.Pan, TouchState.Idle):
            case (TouchState.Eraser, TouchState.Idle):
                ReleaseAll( );
                RestoreMode( );
                break;

            case (TouchState.EvalDraw, TouchState.Draw):
                if (Mode == InkCanvasNextMode.EraseArea)
                {
                    CaptureAll( );
                }

                break;

            case (TouchState.EvalDraw, TouchState.PanZoom):
            case (TouchState.EvalDraw, TouchState.Pan):
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                InitGesture( );
                break;

            case (TouchState.MultiDraw, TouchState.Draw):
                RestoreMode( );
                break;

            case (TouchState.PanZoom, TouchState.Pan):
                InitGesture( );
                break;

            case (TouchState.EvalDraw, TouchState.Eraser):
            case (TouchState.PanZoom, TouchState.Eraser):
            case (TouchState.Pan, TouchState.Eraser):
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.None;
                CaptureAll( );
                break;

            case (TouchState.EvalDraw, TouchState.MultiDraw):
            case (TouchState.Draw, TouchState.MultiDraw):
            case (TouchState.PanZoom, TouchState.MultiDraw):
            case (TouchState.Pan, TouchState.MultiDraw):
            case (TouchState.Eraser, TouchState.MultiDraw):
                ReleaseAll( );
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;
        }

        if (IsAreaEraserActive(state) && !IsAreaEraserActive(newState))
        {
            EndEraserCycle( );
        }

        state = newState;
    }

    private void RestoreMode( )
    {
        switch (prevMode)
        {
            case InkCanvasNextMode.Ink:
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;
            case InkCanvasNextMode.EraseArea:
                Canvas.EditingMode = InkCanvasEditingMode.None;
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
