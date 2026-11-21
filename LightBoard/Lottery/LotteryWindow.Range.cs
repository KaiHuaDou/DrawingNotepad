using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace LightBoard;

// 范围条：两端手柄压在选择条上，手柄里的数字来自 Tag 绑定
public partial class LotteryWindow
{
    private const double RangeCanvasHeight = 34;
    private const double RangeKnobHeight = 30;
    private const double RangeKnobSlot = 30;
    private const double RangeTrackHeight = 8;

    private void RangeCanvasSizeChanged(object o, SizeChangedEventArgs e)
    {
        UpdateRangeVisual( );
    }

    private void RangeThumbDragDelta(object o, DragDeltaEventArgs e)
    {
        if (o is not Thumb thumb)
        {
            return;
        }

        var value = ValueAt(Mouse.GetPosition(RangeCanvas).X);

        if (ReferenceEquals(thumb, RangeLowerThumb))
        {
            viewModel.Lower = value;
        }
        else
        {
            viewModel.Upper = value;
        }
    }

    private void UpdateRangeVisual( )
    {
        var width = RangeCanvas.ActualWidth;
        if (width <= RangeKnobSlot)
        {
            return;
        }

        var lower = PositionOf(viewModel.Lower, width);
        var upper = PositionOf(viewModel.Upper, width);
        const double trackTop = (RangeCanvasHeight - RangeTrackHeight) / 2;

        Canvas.SetLeft(RangeTrackBorder, 0);
        Canvas.SetTop(RangeTrackBorder, trackTop);
        RangeTrackBorder.Width = width;
        RangeTrackBorder.Height = RangeTrackHeight;

        Canvas.SetLeft(RangeFillBorder, lower);
        Canvas.SetTop(RangeFillBorder, trackTop);
        RangeFillBorder.Width = Math.Max(0, upper - lower);
        RangeFillBorder.Height = RangeTrackHeight;

        PlaceThumb(RangeLowerThumb, lower, width);
        PlaceThumb(RangeUpperThumb, upper, width);
    }

    // 手柄宽度随手柄内数字变化，位置按实际宽度居中并夹在条内
    private static void PlaceThumb(Thumb thumb, double center, double width)
    {
        var knob = Math.Max(thumb.ActualWidth, RangeKnobSlot);

        Canvas.SetLeft(thumb, Math.Clamp(center - knob / 2, 0, Math.Max(0, width - knob)));
        Canvas.SetTop(thumb, (RangeCanvasHeight - RangeKnobHeight) / 2);
    }

    private double PositionOf(int value, double width)
    {
        var ratio = viewModel.Max <= 1 ? 0.5 : (value - 1d) / (viewModel.Max - 1);

        return RangeKnobSlot / 2 + ratio * (width - RangeKnobSlot);
    }

    private int ValueAt(double x)
    {
        var usable = RangeCanvas.ActualWidth - RangeKnobSlot;
        if (usable <= 0 || viewModel.Max <= 1)
        {
            return 1;
        }

        var ratio = Math.Clamp((x - RangeKnobSlot / 2) / usable, 0, 1);

        return 1 + (int) Math.Round(ratio * (viewModel.Max - 1));
    }
}
