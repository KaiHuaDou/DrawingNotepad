using System.Windows.Media;

namespace LightBoard.Tests;

public class LruCacheTests
{
    [Fact]
    public void Take_OnMiss_ReturnsNull( )
    {
        var cache = new LruCache(2);

        Assert.Null(cache.Take(0));
    }

    [Fact]
    public void Take_AfterPut_ReturnsSameImage( )
    {
        var cache = new LruCache(2);
        var image = new DrawingImage( );

        cache.Put(3, image);

        Assert.Same(image, cache.Take(3));
    }

    [Fact]
    public void Put_OverCapacity_EvictsLeastRecentlyUsed( )
    {
        var cache = new LruCache(2);
        var first = new DrawingImage( );
        var second = new DrawingImage( );
        var third = new DrawingImage( );

        cache.Put(0, first);
        cache.Put(1, second);
        Assert.Same(second, cache.Take(1));
        cache.Put(2, third);

        Assert.Null(cache.Take(0));
        Assert.Same(second, cache.Take(1));
        Assert.Same(third, cache.Take(2));
    }

    [Fact]
    public void Put_SameIndexTwice_KeepsLatestImage( )
    {
        var cache = new LruCache(2);
        var first = new DrawingImage( );
        var second = new DrawingImage( );

        cache.Put(1, first);
        cache.Put(1, second);

        Assert.Same(second, cache.Take(1));
    }
}
