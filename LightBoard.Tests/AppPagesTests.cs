using System.Windows.Ink;
using System.Windows.Input;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

// App.Pages 一旦被 MainWindow 的缩略图条绑定（ItemsSource={x:Static local:App.Pages}）
// 就会挂上线程亲和的 WPF CollectionView。跨用例执行顺序无保证，因此所有修改统一
// 派发到持有该视图的 UiThread Dispatcher，避免跨线程修改触发 NotSupportedException。
public class AppPagesTests
{
    private static void ResetPages( )
    {
        App.Pages.Clear( );
        App.InitializePages( );
    }

    private static void ResetPages(int count)
    {
        ResetPages( );
        for (var i = 1; i < count; i++)
        {
            App.NewPage( );
        }
    }

    [Fact]
    public void SwitchPage_RejectsOutOfRangeAndSameIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( ); // 单页，当前索引 0

            var changed = 0;
            void counter(object? _o, EventArgs _e)
            {
                changed++;
            }

            App.PageChanged += counter;
            try
            {
                App.SwitchPage(-1);
                App.SwitchPage(1);
                App.SwitchPage(0);

                Assert.Equal(0, App.PageIndex);
                Assert.Equal(0, changed);
            }
            finally
            {
                App.PageChanged -= counter;
            }
        });
    }

    [Fact]
    public void SwitchPage_MovesToValidIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );
            App.NewPage( ); // 共 2 页，当前索引 1

            App.SwitchPage(0);
            Assert.Equal(0, App.PageIndex);

            App.SwitchPage(1);
            Assert.Equal(1, App.PageIndex);
        });
    }

    [Fact]
    public void DeletePage_KeepsLastPage( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );

            App.DeletePage(0);

            Assert.Single(App.Pages);
            Assert.Equal(0, App.PageIndex);
        });
    }

    [Fact]
    public void DeletePage_CurrentPage_ShowsNextPage( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(3); // 当前页为第 2 页
            var next = App.Pages[2];

            App.DeletePage(1);

            Assert.Equal(2, App.Pages.Count);
            Assert.Equal(1, App.PageIndex);
            Assert.Same(next, App.CurrentPage);
            Assert.Equal([1, 2], App.Pages.Select(page => page.Number));
        });
    }

    [Fact]
    public void DeletePage_CurrentLastPage_ShowsPreviousPage( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(2);
            var previous = App.Pages[0];

            App.DeletePage(1);

            Assert.Single(App.Pages);
            Assert.Equal(0, App.PageIndex);
            Assert.Same(previous, App.CurrentPage);
            Assert.Equal(1, App.Pages[0].Number);
        });
    }

    [Fact]
    public void DeletePage_BeforeCurrent_KeepsPageWithoutRebind( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(3);
            var current = App.CurrentPage;

            var changed = 0;
            void counter(object? _o, EventArgs _e)
            {
                changed++;
            }

            App.PageChanged += counter;
            try
            {
                App.DeletePage(0);
            }
            finally
            {
                App.PageChanged -= counter;
            }

            Assert.Equal(1, App.PageIndex);
            Assert.Same(current, App.CurrentPage);
            Assert.Equal(0, changed);
            Assert.Equal([1, 2], App.Pages.Select(page => page.Number));
        });
    }

    [Fact]
    public void DeletePage_AfterCurrent_KeepsIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(3);
            App.SwitchPage(0);
            var current = App.CurrentPage;

            App.DeletePage(2);

            Assert.Equal(2, App.Pages.Count);
            Assert.Equal(0, App.PageIndex);
            Assert.Same(current, App.CurrentPage);
            Assert.Equal([1, 2], App.Pages.Select(page => page.Number));
        });
    }

    [Fact]
    public void IsBoardEmpty_ReturnsTrueForFreshBoard( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );
            Assert.True(App.IsBoardEmpty( ));
        });
    }

    [Fact]
    public void IsBoardEmpty_ReturnsTrueWhenNoPages( )
    {
        UiThread.Run(( ) =>
        {
            App.Pages.Clear( );
            Assert.True(App.IsBoardEmpty( ));
        });
    }

    [Fact]
    public void IsBoardEmpty_ReturnsFalseWhenAnyPageHasStrokes( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );
            var stroke = new Stroke(
                [new StylusPoint(0, 0), new StylusPoint(10, 10)],
                new DrawingAttributes( ));
            App.Pages[0].Strokes.Add(stroke);

            Assert.False(App.IsBoardEmpty( ));
        });
    }
}
