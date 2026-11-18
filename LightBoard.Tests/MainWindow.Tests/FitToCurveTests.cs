namespace LightBoard.Tests.Toolbar;

/// <summary>曲线拟合直接写在笔触属性上，修改后当前工具立即使用新值</summary>
[Collection("Toolbar")]
public class FitToCurveTests
{
    [Fact]
    public void Apply_UpdatesCurrentAndDefaultAttributes( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            host.Window.ApplyFitToCurve(false);

            Assert.False(host.Canvas.DrawingAttributes.DrawingAttributes.FitToCurve);
            Assert.False(host.Canvas.InnerCanvas.DefaultDrawingAttributes.FitToCurve);

            host.Window.ApplyFitToCurve(true);

            Assert.True(host.Canvas.DrawingAttributes.DrawingAttributes.FitToCurve);
            Assert.True(host.Canvas.InnerCanvas.DefaultDrawingAttributes.FitToCurve);
        });
    }
}
