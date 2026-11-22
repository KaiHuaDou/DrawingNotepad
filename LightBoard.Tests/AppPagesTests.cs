using System.Windows.Ink;
using System.Windows.Input;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

// BoardState.Pages 一旦被 MainWindow 的缩略图条绑定（ItemsSource={x:Static local:BoardState.Pages}）
// 就会挂上线程亲和的 WPF CollectionView。跨用例执行顺序无保证，因此所有修改统一
// 派发到持有该视图的 UiThread Dispatcher，避免跨线程修改触发 NotSupportedException。
public class AppPagesTests
{
    private static void ResetPages( )
    {
        BoardState.Pages.Clear( );
        BoardState.InitializePages( );
    }

    private static void ResetPages(int count)
    {
        ResetPages( );
        for (var i = 1; i < count; i++)
        {
            BoardState.NewPage( );
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

            BoardState.PageChanged += counter;
            try
            {
                BoardState.SwitchPage(-1);
                BoardState.SwitchPage(1);
                BoardState.SwitchPage(0);

                Assert.Equal(0, BoardState.PageIndex);
                Assert.Equal(0, changed);
            }
            finally
            {
                BoardState.PageChanged -= counter;
            }
        });
    }

    [Fact]
    public void SwitchPage_MovesToValidIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );
            BoardState.NewPage( ); // 共 2 页，当前索引 1

            BoardState.SwitchPage(0);
            Assert.Equal(0, BoardState.PageIndex);

            BoardState.SwitchPage(1);
            Assert.Equal(1, BoardState.PageIndex);
        });
    }

    [Fact]
    public void DeletePage_KeepsLastPage( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );

            BoardState.DeletePage(0);

            Assert.Single(BoardState.Pages);
            Assert.Equal(0, BoardState.PageIndex);
        });
    }

    [Fact]
    public void DeletePage_CurrentPage_ShowsNextPage( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(3); // 当前页为第 2 页
            var next = BoardState.Pages[2];

            BoardState.DeletePage(1);

            Assert.Equal(2, BoardState.Pages.Count);
            Assert.Equal(1, BoardState.PageIndex);
            Assert.Same(next, BoardState.CurrentPage);
            Assert.Equal([1, 2], BoardState.Pages.Select(page => page.Number));
        });
    }

    [Fact]
    public void DeletePage_CurrentLastPage_ShowsPreviousPage( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(2);
            var previous = BoardState.Pages[0];

            BoardState.DeletePage(1);

            var item = Assert.Single(BoardState.Pages);
            Assert.Equal(0, BoardState.PageIndex);
            Assert.Same(previous, BoardState.CurrentPage);
            Assert.Equal(1, item.Number);
        });
    }

    [Fact]
    public void DeletePage_BeforeCurrent_KeepsPageWithoutRebind( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(3);
            var current = BoardState.CurrentPage;

            var changed = 0;
            void counter(object? _o, EventArgs _e)
            {
                changed++;
            }

            BoardState.PageChanged += counter;
            try
            {
                BoardState.DeletePage(0);
            }
            finally
            {
                BoardState.PageChanged -= counter;
            }

            Assert.Equal(1, BoardState.PageIndex);
            Assert.Same(current, BoardState.CurrentPage);
            Assert.Equal(0, changed);
            Assert.Equal([1, 2], BoardState.Pages.Select(page => page.Number));
        });
    }

    [Fact]
    public void DeletePage_AfterCurrent_KeepsIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages(3);
            BoardState.SwitchPage(0);
            var current = BoardState.CurrentPage;

            BoardState.DeletePage(2);

            Assert.Equal(2, BoardState.Pages.Count);
            Assert.Equal(0, BoardState.PageIndex);
            Assert.Same(current, BoardState.CurrentPage);
            Assert.Equal([1, 2], BoardState.Pages.Select(page => page.Number));
        });
    }

    [Fact]
    public void IsBoardEmpty_ReturnsTrueForFreshBoard( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );
            Assert.True(BoardState.IsBoardEmpty( ));
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
            BoardState.Pages[0].Strokes.Add(stroke);

            Assert.False(BoardState.IsBoardEmpty( ));
        });
    }
}
