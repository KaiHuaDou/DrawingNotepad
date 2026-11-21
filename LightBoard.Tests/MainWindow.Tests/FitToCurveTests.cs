namespace LightBoard.Tests.Toolbar;

/// <summary>曲线拟合直接写在笔触属性上，修改后当前工具立即使用新值</summary>
[Collection("Toolbar")]
public class FitToCurveTests
{
    [Fact]
    public void FitToCurve_UpdatesCurrentAndDefaultAttributes( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            host.Window.FitToCurve = false;

            Assert.False(host.Canvas.DrawingAttributes.DrawingAttributes.FitToCurve);
            Assert.False(host.Canvas.InnerCanvas.DefaultDrawingAttributes.FitToCurve);

            host.Window.FitToCurve = true;

            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.FitToCurve);
            Assert.True(host.Canvas.InnerCanvas.DefaultDrawingAttributes.FitToCurve);
        });
    }
}
