using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Xml.Schema;

namespace InkCanvasNext;

public enum InkCanvasNextMode
{
    Ink,
    EraseStroke,
    EraseArea,
    Select
}

public partial class InkCanvasNext : UserControl
{
#pragma warning disable IDE1006

    private static readonly DependencyPropertyKey CanRedoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanRedo),
            typeof(bool),
            typeof(InkCanvasNext),
            new PropertyMetadata(false));

    private static readonly DependencyPropertyKey CanUndoPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanUndo),
            typeof(bool),
            typeof(InkCanvasNext),
            new PropertyMetadata(false));

#pragma warning restore IDE1006

    public static readonly DependencyProperty CanRedoProperty = CanRedoPropertyKey.DependencyProperty;

    public static readonly DependencyProperty CanUndoProperty = CanUndoPropertyKey.DependencyProperty;

    public static readonly DependencyProperty DefaultDrawingAttributesProperty =
        DependencyProperty.Register(
            nameof(DefaultDrawingAttributes),
            typeof(DrawingAttributes),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnDefaultDrawingAttributesChanged));

    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.Register(
            nameof(Mode),
            typeof(InkCanvasNextMode),
            typeof(InkCanvasNext),
            new PropertyMetadata(InkCanvasNextMode.Ink, OnModeChanged));

    public static readonly DependencyProperty EraserDiameterProperty =
        DependencyProperty.Register(
            nameof(EraserDiameter),
            typeof(double),
            typeof(InkCanvasNext),
            new PropertyMetadata(50.0));

    public static readonly DependencyProperty StrokesProperty =
        DependencyProperty.Register(
            nameof(Strokes),
            typeof(StrokeCollection),
            typeof(InkCanvasNext),
            new PropertyMetadata(OnStrokesPropertyChanged));

    public InkCanvasNext( )
    {
        InitializeComponent( );

        eraser = new Eraser(Canvas, EraserFeedback);

        Canvas.LayoutTransform = canvasScaleTransform;

        Canvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = Canvas.Strokes;
        DefaultDrawingAttributes = Canvas.DefaultDrawingAttributes;

        prevMode = InkCanvasNextMode.Ink;
        distanceThreshold = 0.6 * SystemParameters.WorkArea.Width;
        distanceThreshold2 = distanceThreshold * distanceThreshold;

        CanvasScroll.ScrollToHorizontalOffset(8192);
        CanvasScroll.ScrollToVerticalOffset(8192);
    }

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanRedoChanged;

    public event EventHandler<DependencyPropertyChangedEventArgs>? CanUndoChanged;

    public event EventHandler? StrokesChanged;

    public bool CanRedo
    {
        get => (bool) GetValue(CanRedoProperty);
        private set => SetValue(CanRedoPropertyKey, value);
    }

    public bool CanUndo
    {
        get => (bool) GetValue(CanUndoProperty);
        private set => SetValue(CanUndoPropertyKey, value);
    }

    public DrawingAttributes DefaultDrawingAttributes
    {
        get => (DrawingAttributes) GetValue(DefaultDrawingAttributesProperty);
        set => SetValue(DefaultDrawingAttributesProperty, value);
    }

    public InkCanvasNextMode Mode
    {
        get => (InkCanvasNextMode) GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double EraserDiameter
    {
        get => (double) GetValue(EraserDiameterProperty);
        set => SetValue(EraserDiameterProperty, value);
    }

    public StrokeCollection Strokes
    {
        get => (StrokeCollection) GetValue(StrokesProperty);
        set => SetValue(StrokesProperty, value);
    }

    public double CurrentScale
    {
        get => currentScale;
        set
        {
            currentScale = value;
            smoothedScale = value;
            canvasScaleTransform.ScaleX = canvasScaleTransform.ScaleY = value;
            eraser.Scale = value;
        }
    }

    public double OffsetX
    {
        get => CanvasScroll.HorizontalOffset;
        set => CanvasScroll.ScrollToHorizontalOffset(value);
    }

    public double OffsetY
    {
        get => CanvasScroll.VerticalOffset;
        set => CanvasScroll.ScrollToVerticalOffset(value);
    }

    private static void OnDefaultDrawingAttributesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is DrawingAttributes attributes)
        {
            (d as InkCanvasNext)?.Canvas.DefaultDrawingAttributes = attributes;
        }
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as InkCanvasNext)?.ApplyEditingMode((InkCanvasNextMode) e.NewValue);
    }

    private static void OnStrokesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        (d as InkCanvasNext)?.ApplyStrokes(e.NewValue as StrokeCollection);
    }

    private void ApplyEditingMode(InkCanvasNextMode mode)
    {
        prevMode = mode;
        if (state != TouchState.Idle)
        {
            return;
        }

        switch (mode)
        {
            case InkCanvasNextMode.Ink:
                Canvas.EditingMode = InkCanvasEditingMode.Ink;
                break;
            case InkCanvasNextMode.EraseStroke:
                Canvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                break;
            case InkCanvasNextMode.EraseArea:
                Canvas.EditingMode = InkCanvasEditingMode.None;
                break;
            case InkCanvasNextMode.Select:
                Canvas.EditingMode = InkCanvasEditingMode.Select;
                break;
        }
    }

    private void ApplyStrokes(StrokeCollection? strokes)
    {
        var newStrokes = strokes ?? [];

        Canvas.Strokes.StrokesChanged -= OnStrokesChanged;
        Canvas.Strokes = newStrokes;
        Canvas.Strokes.StrokesChanged += OnStrokesChanged;

        Strokes = newStrokes;

        ClearHistory( );
    }
}
