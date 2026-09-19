using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

[Collection("Toolbar")]
public class ToolbarSequenceTests
{
    private static readonly Color Red = Color.FromRgb(0xFF, 0x43, 0x45);

    [Fact]
    public void LongSequence_ConsistentEachStep( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );
            var window = host.Window;

            ToolEntry.Enter(host, InkCanvasNextMode.Select);
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.ClickRadio(host, window.LineRadio);
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.ClickRadio(host, window.CircleRadio);
            ToolbarAssertions.AssertConsistent(host);

            ToolEntry.Enter(host, InkCanvasNextMode.Highlighter);
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.ClickRadio(host, host.FindColor(Red));
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.ClickRadio(host, host.FindThickness(10));
            ToolbarAssertions.AssertConsistent(host);

            ToolEntry.Enter(host, InkCanvasNextMode.EraseArea);
            ToolbarAssertions.AssertConsistent(host);

            ToolEntry.Enter(host, InkCanvasNextMode.Highlighter);
            ToolbarAssertions.AssertConsistent(host);

            ToolDriver.Toggle(window.HighLighterToggle, false);
            ToolbarAssertions.AssertConsistent(host);

            // 高亮退出后仍在 EraseArea（快照还原），再点一次当前模式回退到画笔
            ToolDriver.ClickRadio(host, window.EraseAreaRadio);
            ToolbarAssertions.AssertConsistent(host);
        });
    }

    [Fact]
    public void AllModes_WithColorAndThickness_ConsistentEachStep( )
    {
        UiThread.Run(( ) =>
        {
            using var host = new MainWindowHost( );

            foreach (var mode in new[]
            {
                InkCanvasNextMode.Ink,
                InkCanvasNextMode.Line,
                InkCanvasNextMode.Circle,
                InkCanvasNextMode.EraseStroke,
                InkCanvasNextMode.EraseArea,
                InkCanvasNextMode.Select,
            })
            {
                ToolEntry.Enter(host, mode);
                ToolbarAssertions.AssertConsistent(host);

                ToolDriver.ClickRadio(host, host.FindColor(Red));
                ToolDriver.ClickRadio(host, host.FindThickness(5));
                ToolbarAssertions.AssertConsistent(host);
            }
        });
    }
}