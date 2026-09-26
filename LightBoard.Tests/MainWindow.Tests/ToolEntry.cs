using InkCanvasNext;

namespace LightBoard.Tests.Toolbar;

internal static class ToolEntry
{
    public static void Enter(MainWindowHost host, InkCanvasNextMode mode)
    {
        switch (mode)
        {
            case InkCanvasNextMode.Ink:
                break;
            case InkCanvasNextMode.EraseStroke:
                ToolDriver.ClickRadio(host, host.Window.EraseStrokeRadio);
                break;
            case InkCanvasNextMode.EraseArea:
                ToolDriver.ClickRadio(host, host.Window.EraseAreaRadio);
                break;
            case InkCanvasNextMode.Select:
                ToolDriver.ClickRadio(host, host.Window.SelectRadio);
                break;
            case InkCanvasNextMode.Line:
                ToolDriver.ClickRadio(host, host.Window.LineRadio);
                break;
            case InkCanvasNextMode.Circle:
                ToolDriver.ClickRadio(host, host.Window.CircleRadio);
                break;
            case InkCanvasNextMode.Highlighter:
                ToolDriver.Toggle(host.Window.HighlighterToggle, true);
                break;
        }
    }
}