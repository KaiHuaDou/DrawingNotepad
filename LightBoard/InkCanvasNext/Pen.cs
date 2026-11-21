using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InkCanvasNext;

// 手写笔输入路径：原生工具放行给 InkCanvas，自定义工具经 PenPointer 进入触摸状态机
public partial class InkCanvasNext
{
    private readonly InputArbiter arbiter = new( );

    private bool penInRange;
    private bool penCaptured;

    internal SyntheticTouchDevice PenPointer { get; private set; } = null!;

    private void CanvasPreviewStylusDown(object o, StylusDownEventArgs e)
    {
        HidePenCursor( );

        if (e.StylusDevice is not { } device)
        {
            return;
        }

        arbiter.RefreshTabletDevices( );

        var kind = InputArbiter.KindOf(device);

        if (kind == InputKind.Touch)
        {
            if (arbiter.TouchCount == 0)
            {
                CancelLower(InputKind.Touch);
            }

            return;
        }

        if (kind == InputKind.Mouse)
        {
            BeginMouseContact(e.GetPosition(this), e);
            return;
        }

        if (!arbiter.Accepts(InputKind.Stylus))
        {
            arbiter.Begin(InputKind.Stylus);
            e.Handled = true;
            return;
        }

        if (touches.Count > 0)
        {
            ResetTouchState( );
        }

        arbiter.Begin(InputKind.Stylus);
        e.Handled = BeginPenContact(e.Inverted, e.GetPosition(this));
    }

    private bool BeginPenContact(bool inverted, Point position)
    {
        if (barrelRestorePending)
        {
            barrelRestorePending = false;
            ApplyModeToEditing(DrawingAttributes.Mode);
        }

        penContact++;

        if (BeginBarrelErase(inverted))
        {
            return false;
        }

        if (UsesPenTouch(inverted))
        {
            InnerCanvas.CaptureStylus( );
            penCaptured = InnerCanvas.IsStylusCaptured;
            PenPointer.Down(position);
            return true;
        }

        if (IsNativeEraseContact(inverted))
        {
            nativeEraseToken = BeginNativeErase( );
        }

        return false;
    }

    internal bool UsesPenTouch(bool inverted)
    {
        return !IsReadOnly && (Snapshot.IsAreaErase || (!inverted && Snapshot.IsCustomTool));
    }

    private void CanvasPreviewStylusMove(object o, StylusEventArgs e)
    {
        if (e.StylusDevice is not { } device)
        {
            return;
        }

        var kind = InputArbiter.KindOf(device);

        if (kind == InputKind.Touch)
        {
            HidePenCursor( );
            return;
        }

        if (kind == InputKind.Mouse)
        {
            HidePenCursor( );

            if (!arbiter.MouseAccepted)
            {
                if (arbiter.MouseDown)
                {
                    e.Handled = true;
                }

                return;
            }

            if (MousePointer.IsDown)
            {
                MousePointer.Move(e.GetPosition(this));
                e.Handled = true;
            }

            return;
        }

        if (!arbiter.PenAccepted)
        {
            if (arbiter.PenDown)
            {
                e.Handled = true;
            }

            return;
        }

        if (PenPointer.IsDown)
        {
            PenPointer.Move(e.GetPosition(this));
            e.Handled = true;
            return;
        }

        if (penInRange && e.InAir)
        {
            UpdatePenCursor(e.GetPosition(this));
        }
    }

    private void CanvasPreviewStylusUp(object o, StylusEventArgs e)
    {
        HidePenCursor( );

        if (e.StylusDevice is not { } device)
        {
            return;
        }

        var kind = InputArbiter.KindOf(device);

        if (kind == InputKind.Touch)
        {
            return;
        }

        if (kind == InputKind.Mouse)
        {
            var accepted = arbiter.MouseAccepted;

            if (MousePointer.IsDown)
            {
                MousePointer.Up(e.GetPosition(this));
                e.Handled = true;
            }

            arbiter.End(InputKind.Mouse);

            if (!accepted)
            {
                e.Handled = true;
            }

            return;
        }

        var penAccepted = arbiter.PenAccepted;
        var synthetic = PenPointer.IsDown;

        if (synthetic)
        {
            PenPointer.Up(e.GetPosition(this));
            e.Handled = true;
        }

        arbiter.End(InputKind.Stylus);
        EndPenContact( );
        ReleasePenCapture( );

        if (!penAccepted)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 释放笔在自定义工具下自行取得的 Stylus 捕获；WPF 原生命中不得由这里释放
    /// </summary>
    private void ReleasePenCapture( )
    {
        if (!penCaptured)
        {
            return;
        }

        penCaptured = false;

        if (InnerCanvas.IsStylusCaptured)
        {
            InnerCanvas.ReleaseStylusCapture( );
        }
    }

    /// <summary>
    /// 合成触点序列结束：同步结束对应输入的仲裁状态，避免窗口失活后残留按下
    /// </summary>
    private void EndPointerContact(SyntheticTouchDevice pointer)
    {
        if (ReferenceEquals(pointer, MousePointer))
        {
            arbiter.End(InputKind.Mouse);
            return;
        }

        if (ReferenceEquals(pointer, PenPointer))
        {
            arbiter.End(InputKind.Stylus);
            EndPenContact( );
        }
    }

    private void CanvasPreviewStylusInRange(object o, StylusEventArgs e)
    {
        if (e.StylusDevice is not { } device || InputArbiter.KindOf(device) != InputKind.Stylus)
        {
            return;
        }

        penInRange = true;
    }

    private void CanvasPreviewStylusOutOfRange(object o, StylusEventArgs e)
    {
        if (e.StylusDevice is not { } device || InputArbiter.KindOf(device) != InputKind.Stylus)
        {
            return;
        }

        penInRange = false;
        barrelPressed = false;
        HidePenCursor( );

        if (arbiter.PenDown)
        {
            CancelNativeStroke( );
            ResetTouchState( );
        }
    }

    private void CanvasPreviewStylusInAirMove(object o, StylusEventArgs e)
    {
        if (e.StylusDevice is not { } device || InputArbiter.KindOf(device) != InputKind.Stylus)
        {
            return;
        }

        if (penInRange && !arbiter.PenDown)
        {
            UpdatePenCursor(e.GetPosition(this));
        }
    }

    private void CanvasLostStylusCapture(object o, StylusEventArgs e)
    {
        penCaptured = false;
        HidePenCursor( );

        if (arbiter.MouseDown || MousePointer.IsDown)
        {
            if (MousePointer.IsDown)
            {
                MousePointer.Reset( );
            }

            ResetTouchState( );
            return;
        }

        if (arbiter.PenDown || PenPointer.IsDown)
        {
            ResetTouchState( );
        }
    }

    private void CancelLower(InputKind accepted)
    {
        if (accepted == InputKind.Touch && (arbiter.MouseDown || arbiter.PenDown))
        {
            ResetTouchState( );
            CancelNativeStroke( );
            return;
        }

        if (accepted == InputKind.Mouse && arbiter.PenDown)
        {
            ResetTouchState( );
            CancelNativeStroke( );
        }
    }

    private void CancelNativeStroke( )
    {
        if ((!InnerCanvas.IsStylusCaptured && !InnerCanvas.IsMouseCaptured)
            || InnerCanvas.EditingMode == InkCanvasEditingMode.None)
        {
            return;
        }

        var mode = InnerCanvas.EditingMode;
        InnerCanvas.EditingMode = InkCanvasEditingMode.None;
        InnerCanvas.EditingMode = mode;
    }
}
