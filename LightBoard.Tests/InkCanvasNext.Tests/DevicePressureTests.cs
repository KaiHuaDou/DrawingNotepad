namespace InkCanvasNext.Tests;

/// <summary>设备压力反解：WPF 宽度倍率 1.5p' + 0.25 反算成设备压力</summary>
public class DevicePressureTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0f)]
    [InlineData(0.5f, 1f / 6f)]
    [InlineData(1f, 0.5f)]
    public void Map_UseDevicePressure(float pressure, float expected)
    {
        Assert.Equal(expected, DevicePressureStylusPlugIn.Map(pressure, true), 5);
    }

    [Fact]
    public void Map_FixedPressure( )
    {
        Assert.Equal(0.5f, DevicePressureStylusPlugIn.Map(0f, false));
        Assert.Equal(0.5f, DevicePressureStylusPlugIn.Map(0.5f, false));
        Assert.Equal(0.5f, DevicePressureStylusPlugIn.Map(1f, false));
    }

    [Fact]
    public void Map_ClampsToRange( )
    {
        Assert.Equal(0f, DevicePressureStylusPlugIn.Map(-1f, true));
        Assert.Equal(1f, DevicePressureStylusPlugIn.Map(2f, true));
    }

    [Fact]
    public void Plugin_InsertedBeforeRenderer( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            Assert.True(host.Canvas.InnerCanvas.PressurePluginFirst);

            // 湿墨中止会重挂渲染器，压力插件仍须排在它前面
            host.Canvas.ResetTouchState( );
            Assert.True(host.Canvas.InnerCanvas.PressurePluginFirst);
        });
    }
}
