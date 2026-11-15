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

                App.SetPageBackgroundAsync(App.CurrentPage, path).GetAwaiter( ).GetResult( );

                Assert.NotNull(App.CurrentPage.Background);
                Assert.False(App.IsBoardEmpty( ));
                Assert.Contains(App.SnapshotPages( ), p => p.Background is not null);
            });
        }
        finally
        {
            UiThread.Run(App.CloseDocuments);
            File.Delete(path);
        }
    }

    [Fact]
    public void ClearPageBackground_EmptiesBoard( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            var page = App.CurrentPage;
            page.Background = new DocumentBackground(@"C:\a.png", 1, new Rect(0, 0, 10, 10));

            App.ClearPageBackground(page);

            Assert.Null(page.Background);
            Assert.True(App.IsBoardEmpty( ));
        });
    }
}
