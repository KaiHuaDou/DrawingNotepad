using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string fileFilter = "Windows 墨迹文件|*.isf|所有文件|*.*";

    public MainWindow( )
    {
        InitializeComponent( );
        MainCanvas.EraserShape = new RectangleStylusShape(100, 160);
        CanvasScroll.ScrollToHorizontalOffset(3840);
        CanvasScroll.ScrollToVerticalOffset(2160);
        MainCanvas.Strokes.StrokesChanged += OnStrokesChanged;
    }

    private void CloseWindow(object o, RoutedEventArgs e)
    {
        Close( );
    }

    private void MinimizeWindow(object o, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void HighLighterBoxClicked(object o, RoutedEventArgs e)
    {
        MainCanvas.DefaultDrawingAttributes.IsHighlighter = (bool) HighLighterToggle.IsChecked;
    }

    #region IO

    private void OpenFileClick(object o, RoutedEventArgs e)
    {
        OpenFileDialog ofd = new( ) { Filter = fileFilter };
        ofd.ShowDialog( );
        try
        {
            using FileStream fs = new(ofd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            MainCanvas.Strokes.StrokesChanged -= OnStrokesChanged;
            MainCanvas.Strokes = new StrokeCollection(fs);
            MainCanvas.Strokes.StrokesChanged += OnStrokesChanged;
            undoStack.Clear( );
            redoStack.Clear( );
            UpdateUndoRedoButtons( );
        }
        catch { }
    }

    private void SaveFileClick(object o, RoutedEventArgs e)
    {
        SaveFileDialog sfd = new( ) { Filter = fileFilter };
        sfd.ShowDialog( );
        try
        {
            using FileStream fs = new(sfd.FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            MainCanvas.Strokes.Save(fs, false);
        }
        catch { }
    }

    #endregion

    #region Dragging

    private readonly HashSet<int> activeTouches = [];
    private readonly Dictionary<int, Point> touchPoints = [];
    private Point touchCenter;
    private Point scrollStart;
    private InkCanvasEditingMode savedEditingMode = InkCanvasEditingMode.Ink;

    private void MainCanvasPreviewTouchDown(object o, TouchEventArgs e)
    {
        var id = e.TouchDevice.Id;
        activeTouches.Add(id);
        touchPoints[id] = e.GetTouchPoint(CanvasScroll).Position;
        MainCanvas.CaptureTouch(e.TouchDevice);

        if (activeTouches.Count == 2 && MainCanvas.EditingMode != InkCanvasEditingMode.None)
        {
            savedEditingMode = MainCanvas.EditingMode;
            MainCanvas.EditingMode = InkCanvasEditingMode.None;
            touchCenter = CenterOfTouches( );
            scrollStart = new Point(CanvasScroll.HorizontalOffset, CanvasScroll.VerticalOffset);
        }
    }

    private void MainCanvasPreviewTouchMove(object o, TouchEventArgs e)
    {
        if (activeTouches.Count < 2)
        {
            return;
        }

        touchPoints[e.TouchDevice.Id] = e.GetTouchPoint(CanvasScroll).Position;
        Vector delta = CenterOfTouches( ) - touchCenter;
        CanvasScroll.ScrollToHorizontalOffset(scrollStart.X - delta.X);
        CanvasScroll.ScrollToVerticalOffset(scrollStart.Y - delta.Y);
    }

    private void MainCanvasPreviewTouchUp(object o, TouchEventArgs e)
    {
        ReleaseTouch(e.TouchDevice.Id);
    }

    private void MainCanvasPreviewTouchLostCapture(object o, TouchEventArgs e)
    {
        ReleaseTouch(e.TouchDevice.Id);
    }

    private void ReleaseTouch(int id)
    {
        activeTouches.Remove(id);
        touchPoints.Remove(id);

        if (activeTouches.Count < 2 && MainCanvas.EditingMode == InkCanvasEditingMode.None)
        {
            MainCanvas.EditingMode = savedEditingMode;
        }
    }

    private Point CenterOfTouches( )
    {
        return new Point(touchPoints.Values.Average(p => p.X), touchPoints.Values.Average(p => p.Y));
    }

    #endregion

    #region Selection

    private void ColorRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Background: SolidColorBrush brush })
        {
            return;
        }

        MainCanvas.EditingMode = InkCanvasEditingMode.Ink;
        MainCanvas.DefaultDrawingAttributes.Color = brush.Color;
    }

    private void ToolRadioChecked(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "\uED60": MainCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint; break;
            case "\uED61": MainCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke; break;
            case "\uEF20": MainCanvas.EditingMode = InkCanvasEditingMode.Select; break;
        }
    }

    private void EraseAll(object o, RoutedEventArgs e)
    {
        MainCanvas.Strokes.Clear( );
    }

    #endregion

    #region Thickness

    private void ThicknessSliderValueChanged(object o, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MainCanvas is null)
        {
            return;
        }

        var value = (int) ThicknessSlider.Value;
        MainCanvas.DefaultDrawingAttributes.Width = MainCanvas.DefaultDrawingAttributes.Height = value;
    }

    #endregion

    #region UndoRedo

    private sealed class StrokeChange(StrokeCollection added, StrokeCollection removed)
    {
        public StrokeCollection Added { get; } = added;
        public StrokeCollection Removed { get; } = removed;
    }

    private readonly Stack<StrokeChange> undoStack = new( );
    private readonly Stack<StrokeChange> redoStack = new( );
    private bool isApplyingUndoRedo;

    private void OnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
    {
        if (isApplyingUndoRedo)
        {
            return;
        }

        if (e.Added.Count == 0 && e.Removed.Count == 0)
        {
            return;
        }

        undoStack.Push(new StrokeChange(e.Added, e.Removed));
        redoStack.Clear( );
        UpdateUndoRedoButtons( );
    }

    private void UndoButtonClick(object o, RoutedEventArgs e)
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        isApplyingUndoRedo = true;
        StrokeChange change = undoStack.Pop( );
        MainCanvas.Strokes.Remove(change.Added);
        MainCanvas.Strokes.Add(change.Removed);
        isApplyingUndoRedo = false;

        redoStack.Push(change);
        UpdateUndoRedoButtons( );
    }

    private void RedoButtonClick(object o, RoutedEventArgs e)
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        isApplyingUndoRedo = true;
        StrokeChange change = redoStack.Pop( );
        MainCanvas.Strokes.Remove(change.Removed);
        MainCanvas.Strokes.Add(change.Added);
        isApplyingUndoRedo = false;

        undoStack.Push(change);
        UpdateUndoRedoButtons( );
    }

    private void UpdateUndoRedoButtons( )
    {
        UndoButton.IsEnabled = undoStack.Count > 0;
        RedoButton.IsEnabled = redoStack.Count > 0;
    }

    #endregion
}
