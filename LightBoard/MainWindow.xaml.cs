using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

using LightBoard.External;

namespace LightBoard;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer timeTimer;

    internal readonly InkCanvasNext.InkCanvasNext CanvasNext;

    public MainWindow( )
    {
        InitializeComponent( );

        if (Resources["MajorGridBrush"] is DrawingBrush gridBrush)
        {
            gridBrush.Freeze( );
        }

        CanvasNext = new InkCanvasNext.InkCanvasNext(App.CanvasSize, App.InitialOffset)
        {
            Background = MajorGridBrush
        };
        CanvasNext.CanRedoChanged += CanvasNextCanRedoChanged;
        CanvasNext.CanUndoChanged += CanvasNextCanUndoChanged;
        CanvasNext.SelectionChanged += CanvasNextSelectionChanged;
        CanvasNext.StrokesChanged += CanvasNextStrokesChanged;
        MainGrid.Children.Insert(0, CanvasNext);

        colorRadio ??= DefaultColorRadio;
        thicknessRadio ??= DefaultThicknessRadio;
        App.InitializePages( );
        App.PageChanged += OnPageChanged;

        if (App.PendingOpen is not null)
        {
            OpenFile(App.PendingOpen);
        }

        OnPageChanged(this, EventArgs.Empty);

        SyncToolState( );

        CanvasNext.ViewOrSelectionChanged += (_, _) => UpdateSelectionBorderPosition( );
        DebugLayer.Children.Add(new ParametersDebugVisual(CanvasNext));

        timeTimer = new(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Normal,
            (o, e) => TimeText.Text = $"{DateTime.Now:HH:mm}",
            Dispatcher.CurrentDispatcher
        );
        timeTimer.Start( );
    }

    private void CloseWindowClick(object o, RoutedEventArgs e)
    {
        Close( );
    }

    private void MinimizeWindowClick(object o, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void SwitchOutClick(object o, RoutedEventArgs e)
    {

#if DEBUG
        const string ProcessName = "thorium";
#else
        const string ProcessName = "msedge";
#endif

        NativeMethods.SwitchTo(ProcessName);
    }

    private void WindowDeactivated(object o, EventArgs e)
    {
        CanvasNext.ResetTouchState( );
    }

    private void WindowSourceInitialized(object sender, EventArgs e)
    {

        PassThroughBorder.LayoutUpdated += (_, _) => UpdateWindowRegion( );
        LocationChanged += (_, _) => UpdateWindowRegion( );
        SizeChanged += (_, _) => UpdateWindowRegion( );
        Loaded += (_, _) => UpdateWindowRegion( );
    }

    private void UpdateWindowRegion( )
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var hWnd = new WindowInteropHelper(this).Handle;

        if (hWnd == IntPtr.Zero
            || PassThroughBorder == null)
        {
            return;
        }

        if (PassThroughBorder.ActualHeight <= 0
          || PassThroughBorder.ActualWidth <= 0
          || PassThroughBorder.Visibility != Visibility.Visible)
        {
            NativeMethods.ClearWindowRegion(hWnd);
        }

        var transform = PassThroughBorder.TransformToAncestor(this);
        var borderRect = transform.TransformBounds(new Rect(PassThroughBorder.RenderSize));
        borderRect.Inflate(-2, -2);

        NativeMethods.SetWindowRegion(hWnd, ActualWidth, ActualHeight, dpi, borderRect);
    }

    private void DebugLayerClick(object o, RoutedEventArgs e)
    {
        var isChecked = DebugLayerMenu.IsChecked;
        DebugLayer.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
    }
}
