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

    [Fact]
    public void SwitchPage_RejectsOutOfRangeAndSameIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( ); // 单页，当前索引 0
            Assert.False(App.SwitchPage(-1));
            Assert.False(App.SwitchPage(0));
            Assert.False(App.SwitchPage(1));
        });
    }

    [Fact]
    public void SwitchPage_MovesToValidIndex( )
    {
        UiThread.Run(( ) =>
        {
            ResetPages( );
            App.NewPage( ); // 共 2 页，当前索引 1

            Assert.True(App.SwitchPage(0));
            Assert.Equal(0, App.PageIndex);

            Assert.True(App.SwitchPage(1));
            Assert.Equal(1, App.PageIndex);
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