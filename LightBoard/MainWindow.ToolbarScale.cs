using System.Windows.Media;

namespace LightBoard;

public partial class MainWindow
{
    private double toolbarScale = LightBoardSettings.ToolbarScaleDefault;

    private void ApplyToolbarScale( )
    {
        toolbarScale = SettingsStore.Current.ToolbarScale;
        if (toolbarScale == 1)
        {
            return;
        }

        var transform = new ScaleTransform(toolbarScale, toolbarScale);
        transform.Freeze( );

        LeftBorder.LayoutTransform = transform;
        CenterBorder.LayoutTransform = transform;
        RightBorder.LayoutTransform = transform;
        SelectionBorder.LayoutTransform = transform;

        // 缓存的是旧倍率下量得的尺寸
        selectionBorderSize = null;
    }

    /// <summary>
    /// 底侧边缘提示按钮的让位：工具栏高度是本地尺寸，屏幕高度要乘倍率
    /// </summary>
    internal static double EdgeHintGap(double barHeight, double barMarginBottom, double scale)
    {
        return barHeight * scale + barMarginBottom + 9;
    }
}
