using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

using InkCanvasNext;

using LightBoard.External;

namespace LightBoard;

public partial class MainWindow : Window
{
    internal readonly InkCanvasNext.InkCanvasNext CanvasNext;

#if DEBUG
    private const string SwitchOutProcessName = "thorium";
#else
    private const string SwitchOutProcessName = "msedge";
#endif

    private readonly DispatcherTimer timeTimer;

    private TimerWindow? timerWindow;

    public MainWindow( )
    {
        InitializeComponent( );

        // 图标提取要枚举进程并调 Shell，放到窗口显示后再取
        Loaded += (_, _) => Dispatcher.InvokeAsync(RefreshSwitchOutIcon, DispatcherPriority.Background);

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

        Settings.LoadSettings( );
        ApplySettings( );

        colorRadio ??= DefaultColorRadio;
        thicknessRadio ??= DefaultThicknessRadio;
        App.InitializePages( );
        App.PageChanged += OnPageChanged;

        if (App.PendingOpen is { } pendingOpen)
        {
            // 构造期打开会把读盘与背景注册压在首帧之前
            Loaded += (_, _) => Dispatcher.InvokeAsync(( ) => RequestOpenFile(pendingOpen), DispatcherPriority.Background);
        }

        OnPageChanged(this, EventArgs.Empty);

        SyncToolState( );

        CanvasNext.ViewOrSelectionChanged += (_, _) => UpdateSelectionBorderPosition( );
        DebugLayer.Children.Add(new DebugVisual(CanvasNext));

        timeTimer = new(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Normal,
            (o, e) => TimeText.Text = $"{DateTime.Now:HH:mm}",
            Dispatcher.CurrentDispatcher
        );
        timeTimer.Start( );
    }

    private void ApplySettings( )
    {
        var data = Settings.Data;
        CanvasNext.AutoScrollHorizontalThreshold = data.AutoScroll ? InkCanvasNext.InkCanvasNext.AutoScrollHorizontalDefaultThreshold : 0;
        CanvasNext.AutoScrollVerticalThreshold = data.AutoScroll ? InkCanvasNext.InkCanvasNext.AutoScrollVerticalDefaultThreshold : 0;
        var touch = Math.Clamp(data.TouchDisplacementThreshold, Settings.ThresholdMin, Settings.ThresholdMax);
        var panZoom = Math.Clamp(data.PanZoomDisplaceThreshold, Settings.ThresholdMin, Settings.ThresholdMax);
        var pinch = Math.Clamp(data.PinchLockDistance, Settings.ThresholdMin, Settings.ThresholdMax);
        CanvasNext.TouchDisplacementThreshold2 = touch * touch;
        CanvasNext.PanZoomDisplaceThreshold2 = panZoom * panZoom;
        CanvasNext.PinchLockDistance2 = pinch * pinch;
        CanvasNext.SetMaxHistoryCount(Math.Clamp(data.MaxHistoryCount, Settings.HistoryMin, Settings.HistoryMax));
    }

    private void CanvasNextCanRedoChanged(object? o, DependencyPropertyChangedEventArgs e)
    {
        RedoButton.IsEnabled = CanvasNext.CanRedo;
    }

    private void CanvasNextCanUndoChanged(object? o, DependencyPropertyChangedEventArgs e)
    {
        UndoButton.IsEnabled = CanvasNext.CanUndo;
    }

    private void CanvasNextSelectionChanged(object? o, EventArgs e)
    {
        if (CanvasNext.Mode != InkCanvasNextMode.Select || CanvasNext.SelectedCount == 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelectionBorderPosition( );
    }

    private void CanvasNextStrokesChanged(object? o, InkCanvasStrokesChangedEventArgs e)
    {
        Dirty = true;
        App.BoardRevision++;
        App.CurrentPage.InvalidatePreview( );

        if (CanvasNext.Mode != InkCanvasNextMode.Select || CanvasNext.SelectedCount == 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateSelectionBorderPosition( );
    }

    private void CloseWindowClick(object o, RoutedEventArgs e)
    {
        Close( );
    }

    private void MainWindowKeyUp(object o, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ExitStamp( );
        }
    }

    private void MinimizeWindowClick(object o, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void RefreshSwitchOutIcon( )
    {
        SwitchOutButton.Content = ProgramIcon.FromProcess(SwitchOutProcessName, 48);
    }

    private void SwitchOutClick(object o, RoutedEventArgs e)
    {
        NativeMethods.SwitchTo(SwitchOutProcessName);
        RefreshSwitchOutIcon( );
    }

    private void UpdateWindowRegion( )
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var hWnd = new WindowInteropHelper(this).Handle;

        if (hWnd == IntPtr.Zero || PassThroughBorder == null)
        {
            return;
        }

        if (PassThroughBorder.ActualHeight <= 0
          || PassThroughBorder.ActualWidth <= 0
          || PassThroughBorder.Visibility != Visibility.Visible)
        {
            NativeMethods.ClearWindowRegion(hWnd);
            return;
        }

        var transform = PassThroughBorder.TransformToAncestor(this);
        var borderRect = transform.TransformBounds(new Rect(PassThroughBorder.RenderSize));
        borderRect.Inflate(-2, -2);

        NativeMethods.SetWindowRegion(hWnd, ActualWidth, ActualHeight, dpi, borderRect);
    }

    private void WindowDeactivated(object o, EventArgs e)
    {
        CanvasNext.ResetTouchState( );
    }

    private void WindowSourceInitialized(object o, EventArgs e)
    {
        PassThroughBorder.LayoutUpdated += (_, _) => UpdateWindowRegion( );
        LocationChanged += (_, _) => UpdateWindowRegion( );
        SizeChanged += (_, _) => UpdateWindowRegion( );
        Loaded += (_, _) => UpdateWindowRegion( );
    }
}
