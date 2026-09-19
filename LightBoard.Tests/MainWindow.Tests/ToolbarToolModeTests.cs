using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarToolModeTests
{
    private static readonly InkCanvasNextMode[] AllTools =
        [InkCanvasNextMode.EraseStroke, InkCanvasNextMode.EraseArea, InkCanvasNextMode.Select];

    public static TheoryData<InkCanvasNextMode> Tools
    {
        get
        {
            var data = new TheoryData<InkCanvasNextMode>( );
            foreach (var tool in AllTools)
            {
                data.Add(tool);
            }

            return data;
        }
    }

    public static TheoryData<InkCanvasNextMode, InkCanvasNextMode> ToolPairs
    {
        get
        {
            var data = new TheoryData<InkCanvasNextMode, InkCanvasNextMode>( );
            foreach (var from in AllTools)
            {
                foreach (var to in AllTools)
                {
                    if (from != to)
                    {
                        data.Add(from, to);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public void Ink_ClickTool_ActivatesTool(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            ToolEntry.Enter(host, tool);

            Assert.Equal(tool, host.Window.Mode);
            Assert.Equal(tool, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [MemberData(nameof(ToolPairs))]
    public void SwitchToolPairs_StaysInSync(InkCanvasNextMode from, InkCanvasNextMode to)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, from);
            Assert.Equal(from, host.Window.Mode);

            ToolEntry.Enter(host, to);

            Assert.Equal(to, host.Window.Mode);
            Assert.Equal(to, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Theory]
    [InlineData(InkCanvasNextMode.EraseStroke)]
    [InlineData(InkCanvasNextMode.EraseArea)]
    [InlineData(InkCanvasNextMode.Select)]
    public void ReclickTool_ReturnsToInk(InkCanvasNextMode tool)
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, tool);
            Assert.Equal(tool, host.Window.Mode);

            var radio = tool switch
            {
                InkCanvasNextMode.EraseStroke => host.Window.EraseStrokeRadio,
                InkCanvasNextMode.EraseArea => host.Window.EraseAreaRadio,
                _ => host.Window.SelectRadio
            };
            ToolDriver.ClickRadio(host, radio);

            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Ink, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void ReclickTool_ThenClickAgain_Reactivates( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            Assert.Equal(InkCanvasNextMode.Select, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.Window.SelectRadio);
            Assert.Equal(InkCanvasNextMode.Ink, host.Window.Mode);

            ToolDriver.ClickRadio(host, host.Window.SelectRadio);
            Assert.Equal(InkCanvasNextMode.Select, host.Window.Mode);
            Assert.Equal(InkCanvasNextMode.Select, host.Canvas.Mode);
            ToolbarAssertions.AssertConsistent(host);
        });
    }
}
