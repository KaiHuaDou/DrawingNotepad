using System.IO;
using System.Windows;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 单页背景动作：设为背景与清除背景只作用当前页
/// </summary>
[Collection("Toolbar")]
public class BackgroundActionsTests
{
    [Fact]
    public void SetPageBackground_MakesBoardNonEmpty( )
    {
        var path = BackgroundsTests.TempPng( );
        try
        {
            UiThread.Run(( ) =>
            {
                using var host = new MainWindowHost( );

                PageBackgrounds.SetAsync(BoardState.CurrentPage, path, CanvasViewport.Capture(host.Canvas).Box).GetAwaiter( ).GetResult( );

                Assert.NotNull(BoardState.CurrentPage.Background);
                Assert.False(BoardState.IsBoardEmpty( ));
                Assert.Contains(BoardState.SnapshotPages( ), p => p.Background is not null);
            });
        }
        finally
        {
            UiThread.Run(DocumentRegistry.CloseAll);
            File.Delete(path);
        }
    }

    [Fact]
    public void ClearPageBackground_EmptiesBoard( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            var page = BoardState.CurrentPage;
            page.Background = new DocumentBackground(@"C:\a.png", 1, new Rect(0, 0, 10, 10));

            PageBackgrounds.Clear(page);

            Assert.Null(page.Background);
            Assert.True(BoardState.IsBoardEmpty( ));
        });
    }
}
