using System.Windows.Ink;
using System.Windows.Input;

namespace LightBoard.Tests;

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
        ResetPages( ); // 单页，当前索引 0
        Assert.False(App.SwitchPage(-1));
        Assert.False(App.SwitchPage(0));
        Assert.False(App.SwitchPage(1));
    }

    [Fact]
    public void SwitchPage_MovesToValidIndex( )
    {
        ResetPages( );
        App.NewPage( ); // 共 2 页，当前索引 1

        Assert.True(App.SwitchPage(0));
        Assert.Equal(0, App.PageIndex);

        Assert.True(App.SwitchPage(1));
        Assert.Equal(1, App.PageIndex);
    }

    [Fact]
    public void IsBoardEmpty_ReturnsTrueForFreshBoard( )
    {
        ResetPages( );
        Assert.True(App.IsBoardEmpty( ));
    }

    [Fact]
    public void IsBoardEmpty_ReturnsTrueWhenNoPages( )
    {
        App.Pages.Clear( );
        Assert.True(App.IsBoardEmpty( ));
    }

    [Fact]
    public void IsBoardEmpty_ReturnsFalseWhenAnyPageHasStrokes( )
    {
        ResetPages( );
        var stroke = new Stroke(
            [new StylusPoint(0, 0), new StylusPoint(10, 10)],
            new DrawingAttributes( ));
        App.Pages[0].Strokes.Add(stroke);

        Assert.False(App.IsBoardEmpty( ));
    }
}
