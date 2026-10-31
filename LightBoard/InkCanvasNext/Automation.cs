using System.Windows.Automation.Peers;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    protected override AutomationPeer OnCreateAutomationPeer( )
    {
        return new InkCanvasNextAutomationPeer(this);
    }
}

internal sealed class InkCanvasNextAutomationPeer(InkCanvasNext owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore( )
    {
        return nameof(InkCanvasNext);
    }

    protected override AutomationControlType GetAutomationControlTypeCore( )
    {
        return AutomationControlType.Pane;
    }
}
