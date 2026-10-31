using System.Windows.Automation.Peers;

namespace InkCanvasNext.Tests;

/// <summary>控件自动化 Peer。</summary>
public class AutomationTests
{
    [Fact]
    public void CreatePeer_PaneWithClassName( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );
            var peer = UIElementAutomationPeer.CreatePeerForElement(host.Canvas);

            Assert.IsType<InkCanvasNextAutomationPeer>(peer);
            Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType( ));
            Assert.Equal(nameof(InkCanvasNext), peer.GetClassName( ));
        });
    }
}
