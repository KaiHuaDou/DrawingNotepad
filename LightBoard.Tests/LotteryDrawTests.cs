namespace LightBoard.Tests;

/// <summary>抽选下标：等概率取不重复下标，数量按总数夹紧</summary>
public class LotteryDrawTests
{
    [Fact]
    public void PickIndices_ReturnsDistinctIndices( )
    {
        var indices = LotteryDraw.PickIndices(new Random(7), 51, 5);

        Assert.Equal(5, indices.Length);
        Assert.Equal(5, indices.Distinct( ).Count( ));
        Assert.All(indices, index => Assert.InRange(index, 0, 50));
    }

    [Fact]
    public void PickIndices_ClampsCount( )
    {
        Assert.Empty(LotteryDraw.PickIndices(new Random(1), 10, 0));
        Assert.Equal(10, LotteryDraw.PickIndices(new Random(1), 10, 99).Length);
    }

    [Fact]
    public void PickIndices_FullCountReturnsAll( )
    {
        var indices = LotteryDraw.PickIndices(new Random(3), 10, 10);

        Assert.Equal(Enumerable.Range(0, 10), indices.Order( ));
    }

    [Fact]
    public void PickIndices_SeedReproducible( )
    {
        var first = LotteryDraw.PickIndices(new Random(42), 51, 6);
        var second = LotteryDraw.PickIndices(new Random(42), 51, 6);

        Assert.Equal(first, second);
    }

    [Fact]
    public void PickIndices_EmptyPool( )
    {
        Assert.Empty(LotteryDraw.PickIndices(new Random(1), 0, 3));
    }
}
