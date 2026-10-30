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

        // 构造期设置（橡皮参数）要在建画布前读入
        Settings.LoadSettings( );

        CanvasNext = new InkCanvasNext.InkCanvasNext(App.CanvasSize, App.InitialOffset, Settings.Data.CanvasOptions)
        {
            Background = MajorGridBrush
        };
        CanvasNext.CanRedoChanged += CanvasNextCanRedoChanged;
        CanvasNext.CanUndoChanged += CanvasNextCanUndoChanged;
        CanvasNext.SelectionChanged += CanvasNextSelectionChanged;
        CanvasNext.StrokesChanged += CanvasNextStrokesChanged;
        MainGrid.Children.Insert(0, CanvasNext);

        var edgeHintMargin = InkCanvasNext.InkCanvasNext.EdgeHintMarginDefault;
        CanvasNext.EdgeHintMargin = new Thickness(edgeHintMargin, edgeHintMargin, edgeHintMargin, EdgeHintBottomMargin( ));
        CenterBorder.SizeChanged += CenterBorderSizeChanged;

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
        HelpLayer.Children.Add(CreateHelpVisual( ));

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
        CanvasNext.EdgeScrollTopThreshold = data.EdgeScrollHint ? data.EdgeScrollTopThreshold : 0;
        CanvasNext.EdgeScrollBottomThreshold = data.EdgeScrollHint ? data.EdgeScrollBottomThreshold : 0;
        CanvasNext.EdgeScrollLeftThreshold = data.EdgeScrollHint ? data.EdgeScrollLeftThreshold : 0;
        CanvasNext.EdgeScrollRightThreshold = data.EdgeScrollHint ? data.EdgeScrollRightThreshold : 0;
        CanvasNext.TouchDisplacementThreshold2 = data.TouchDisplacementThreshold * data.TouchDisplacementThreshold;
        CanvasNext.PanZoomDisplaceThreshold2 = data.PanZoomDisplaceThreshold * data.PanZoomDisplaceThreshold;
        CanvasNext.PinchLockDistance2 = data.PinchLockDistance * data.PinchLockDistance;
        CanvasNext.SetMaxHistoryCount(data.MaxHistoryCount);
    }

    /// <summary>
    /// 底侧提示按钮让开主工具栏：收起与展开的高度都由 Border 实际高度给出，另加外边距与 9 DIP 间隙。
    /// </summary>
    private double EdgeHintBottomMargin( )
    {
        return CenterBorder.ActualHeight + CenterBorder.Margin.Bottom + 9;
    }

    private void CenterBorderSizeChanged(object o, SizeChangedEventArgs e)
    {
        var edge = InkCanvasNext.InkCanvasNext.EdgeHintMarginDefault;
        CanvasNext.EdgeHintMargin = new Thickness(edge, edge, edge, EdgeHintBottomMargin( ));
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
