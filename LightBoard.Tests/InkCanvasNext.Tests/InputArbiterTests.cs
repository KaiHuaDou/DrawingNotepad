namespace InkCanvasNext.Tests;

/// <summary>输入仲裁：触摸高于鼠标，鼠标高于手写笔；被拒绝的输入需重新按下</summary>
public class InputArbiterTests
{
    [Fact]
    public void Accepts_Empty( )
    {
        var arbiter = new InputArbiter( );

        Assert.True(arbiter.Accepts(InputKind.Touch));
        Assert.True(arbiter.Accepts(InputKind.Mouse));
        Assert.True(arbiter.Accepts(InputKind.Stylus));
    }

    [Fact]
    public void Touch_PreemptsMouseAndStylus( )
    {
        var arbiter = new InputArbiter( );
        arbiter.Begin(InputKind.Touch, 1);

        Assert.False(arbiter.Accepts(InputKind.Mouse));
        Assert.False(arbiter.Accepts(InputKind.Stylus));
    }

    [Fact]
    public void Mouse_PreemptsStylus( )
    {
        var arbiter = new InputArbiter( );
        arbiter.Begin(InputKind.Mouse);

        Assert.True(arbiter.MouseAccepted);
        Assert.False(arbiter.Accepts(InputKind.Stylus));
    }

    [Fact]
    public void RejectedLower_StaysRejectedUntilRepress( )
    {
        var arbiter = new InputArbiter( );
        arbiter.Begin(InputKind.Touch, 1);

        Assert.False(arbiter.Begin(InputKind.Mouse));
        Assert.False(arbiter.MouseAccepted);

        arbiter.End(InputKind.Touch, 1);
        Assert.False(arbiter.Allows(InputKind.Mouse));

        arbiter.End(InputKind.Mouse);
        Assert.True(arbiter.Accepts(InputKind.Mouse));
        Assert.True(arbiter.Begin(InputKind.Mouse));
        Assert.True(arbiter.Allows(InputKind.Mouse));
    }

    [Fact]
    public void StrokeKind_LastAccepted( )
    {
        var arbiter = new InputArbiter( );

        arbiter.Begin(InputKind.Touch, 1);
        Assert.Equal(InputKind.Touch, arbiter.StrokeKind);

        arbiter.End(InputKind.Touch, 1);
        arbiter.Begin(InputKind.Mouse);
        Assert.Equal(InputKind.Mouse, arbiter.StrokeKind);

        arbiter.End(InputKind.Mouse);
        arbiter.Begin(InputKind.Stylus);
        Assert.Equal(InputKind.Stylus, arbiter.StrokeKind);
    }

    [Fact]
    public void Allows_LowerPriorityWhileTouch( )
    {
        var arbiter = new InputArbiter( );
        arbiter.Begin(InputKind.Touch, 1);

        Assert.True(arbiter.Allows(InputKind.Touch));
        Assert.False(arbiter.Allows(InputKind.Mouse));
        Assert.False(arbiter.Allows(InputKind.Stylus));
    }

    [Fact]
    public void DeviceIds_Zero_IsUnknownForTabletLookup( )
    {
        var arbiter = new InputArbiter( );

        Assert.False(arbiter.IsTouchTabletId(0));
        Assert.False(arbiter.IsStylusTabletId(0));
    }

    [Fact]
    public void KindOf_NullDevice_IsMouse( )
    {
        // WPF 的 DynamicRenderer.Reset 以 null 表示鼠标
        Assert.Equal(InputKind.Mouse, InputArbiter.KindOf(null));
    }
}
