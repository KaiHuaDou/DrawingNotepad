using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace InkCanvasNext;

// 笔杆按钮与原生擦除：落笔前按住笔杆按笔擦，一次接触的原生擦除合成一个撤销批次
public partial class InkCanvasNext
{
    private int penContact;
    private int nativeEraseToken;
    private bool barrelPressed;
    private bool barrelErase;
    private bool barrelRestorePending;

    private void CanvasPreviewStylusButtonDown(object o, StylusButtonEventArgs e)
    {
        if (e.StylusDevice is not { } device || InputArbiter.KindOf(device) != InputKind.Stylus)
        {
            return;
        }

        if (e.StylusButton.Guid == StylusPointProperties.BarrelButton.Id)
        {
            barrelPressed = true;
        }
    }

    private void CanvasPreviewStylusButtonUp(object o, StylusButtonEventArgs e)
    {
        if (e.StylusDevice is not { } device || InputArbiter.KindOf(device) != InputKind.Stylus)
        {
            return;
        }

        if (e.StylusButton.Guid == StylusPointProperties.BarrelButton.Id)
        {
            barrelPressed = false;
        }
    }

    private bool BeginBarrelErase(bool inverted)
    {
        if (!options.Pen.BarrelErase || !barrelPressed || IsReadOnly)
        {
            return false;
        }

        barrelErase = true;
        InnerCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;

        if (inverted)
        {
            InnerCanvas.EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
        }

        nativeEraseToken = BeginNativeErase( );
        return true;
    }

    private bool IsNativeEraseContact(bool inverted)
    {
        return !IsReadOnly
            && (barrelErase || inverted || DrawingAttributes.Mode == InkCanvasNextMode.EraseStroke);
    }

    private void EndPenContact( )
    {
        var contact = penContact;

        if (barrelErase)
        {
            barrelErase = false;
            barrelRestorePending = true;
            ScheduleBarrelRestore(contact);
        }

        if (nativeEraseToken != 0)
        {
            var token = nativeEraseToken;
            nativeEraseToken = 0;
            ScheduleNativeEraseEnd(token);
        }
    }

    private void ScheduleBarrelRestore(int contact)
    {
        Dispatcher.InvokeAsync(( ) =>
        {
            if (penContact != contact)
            {
                return;
            }

            barrelRestorePending = false;
            ApplyModeToEditing(DrawingAttributes.Mode);
        }, DispatcherPriority.Input);
    }

    private void ScheduleNativeEraseEnd(int token)
    {
        Dispatcher.InvokeAsync(( ) => CompleteNativeErase(token), DispatcherPriority.Input);
    }
}
