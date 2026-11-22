using System;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Win32;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public partial class MainWindow
{
    private async void SetBackgroundClick(object o, RoutedEventArgs e)
    {
        var page = BoardState.CurrentPage;
        if (page.Background is not null && !WhetherReplaceBackground( ))
        {
            return;
        }

        OpenFileDialog dialog = new( ) { Filter = ImageFilter };
        if (dialog.ShowDialog( ) != true)
        {
            return;
        }

        await ApplyPageBackgroundAsync(page, dialog.FileName);
    }

    private async Task ApplyPageBackgroundAsync(Page page, string path)
    {
        try
        {
            await RunWithLoading("正在设置背景", ( ) => PageBackgrounds.SetAsync(page, path, CanvasViewport.Capture(CanvasNext).Box));
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowDetailedInfo("无法设置背景", ex.Message, $"{ex.Message}\n{ex.StackTrace}");
            return;
        }

        SyncPageBackground( );
        Dirty = true;
    }

    private void ClearBackgroundClick(object o, RoutedEventArgs e)
    {
        var page = BoardState.CurrentPage;
        if (page.Background is null)
        {
            return;
        }

        if (!WhetherClearBackground( ))
        {
            return;
        }

        PageBackgrounds.Clear(page);
        SyncPageBackground( );
        Dirty = true;
    }

    // 换背景或清背景后刷新画布：视频页落回摄像头画面，其余页落回页自己的背景
    private void SyncPageBackground( )
    {
        var page = BoardState.CurrentPage;
        CanvasNext.SetDocumentPage(DocumentRegistry.Image(page.Background), page.Background?.Box);

#if OPENCV
        SyncVideoOverlay( );
#endif
    }

    private static bool WhetherReplaceBackground( )
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "当前页已有背景，替换？",
            MainIcon = TaskDialogIcon.Information,
            ButtonStyle = TaskDialogButtonStyle.CommandLinks
        };

        var replaceButton = new TaskDialogButton("替换");
        var cancelButton = new TaskDialogButton("取消");
        dialog.Buttons.Add(replaceButton);
        dialog.Buttons.Add(cancelButton);

        return dialog.ShowDialog( ) == replaceButton;
    }

    private static bool WhetherClearBackground( )
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "清除当前页的背景？",
            MainIcon = TaskDialogIcon.Information,
            ButtonStyle = TaskDialogButtonStyle.CommandLinks
        };

        var clearButton = new TaskDialogButton("清除");
        var cancelButton = new TaskDialogButton("取消");
        dialog.Buttons.Add(clearButton);
        dialog.Buttons.Add(cancelButton);

        return dialog.ShowDialog( ) == clearButton;
    }
}
