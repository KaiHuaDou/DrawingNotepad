using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using LightBoard.External;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow : Window
{
    private const string FileFilter =
        "可打开的文件|*.lbf;*.isf;*.pptx;*.ppt;*.docx;*.doc;*.xps;*.pdf;*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff|轻白板文件|*.lbf|Windows 墨迹文件|*.isf|演示文稿|*.pptx;*.ppt|Word 文档|*.docx;*.doc|XPS 文档|*.xps|PDF 文档|*.pdf|图片|*.bmp;*.gif;*.ico;*.jpg;*.jpeg;*.png;*.tiff|所有文件|*.*";

    private readonly DispatcherTimer timeTimer;

    public MainWindow( )
    {
        InitializeComponent( );

        if (Resources["MajorGridBrush"] is DrawingBrush gridBrush)
        {
            gridBrush.Freeze( );
        }

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

        timeTimer = new(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Normal,
            (o, e) => TimeText.Text = $"{DateTime.Now:HH:mm}",
            Dispatcher.CurrentDispatcher
        );
        timeTimer.Start( );
    }

    private bool Dirty
    {
        get => field && !App.IsBoardEmpty( );
        set;
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

    private bool WhetherCloseFile( )
    {
        if (!Dirty)
        {
            return false;
        }

        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "有未保存的墨迹，是否保存？",
            MainIcon = TaskDialogIcon.Information,
            ButtonStyle = TaskDialogButtonStyle.CommandLinks
        };

        var fastSaveButton = new TaskDialogButton("快速保存");
        var saveButton = new TaskDialogButton("手动保存");
        var discardButton = new TaskDialogButton("放弃");
        var cancelButton = new TaskDialogButton("取消");
        dialog.Buttons.Add(fastSaveButton);
        dialog.Buttons.Add(saveButton);
        dialog.Buttons.Add(discardButton);
        dialog.Buttons.Add(cancelButton);
        var result = dialog.ShowDialog( );

        if (result == fastSaveButton)
        {
            BoardFile.Write(Path.Join(App.AppPath, "fastsave", $"{DateTime.Now:yyyyMMdd-HHmmss}.lbf"), App.Pages);
            Dirty = false;
            return false;
        }
        else if (result == saveButton)
        {
            return !SaveFile( );
        }
        else if (result == discardButton)
        {
            return false;
        }

        return true;
    }

    private void WindowClosing(object o, CancelEventArgs e)
    {
        e.Cancel = WhetherCloseFile( );
    }

    private void WindowDeactivated(object o, EventArgs e)
    {
        CanvasNext.ResetTouchState( );
    }
}
