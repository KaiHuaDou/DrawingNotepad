using System.Windows;

namespace LightBoard;

public partial class MainWindow
{
    private bool moreExpandedByHelp;

    private HelpVisual CreateHelpVisual( )
    {
        return new HelpVisual(
            MainGrid,
            [
                [
                    new(ExitButton, "关闭"),
                    new(WindowMinimizeButton, "最小化"),
                    new(MoreButton, "文件"),
                    new(BackgroundButton, "背景"),
                    new(SwitchOutButton, "切换窗口"),
                ],
                [
                    // 主工具栏两行同组：标签都排在工具栏上方，下排按钮的箭头跨过上排
                    new(DefaultThicknessRadio, "粗细"),
                    new(LineRadio, "直线"),
                    new(CircleRadio, "圆"),
                    new(SelectRadio, "选择"),
                    new(PasteButton, "粘贴"),
                    new(DefaultColorRadio, "颜色（点选即绘图）"),
                    new(HighlighterToggle, "荧光笔（仅黄色）"),
                    new(EraseAreaRadio, "橡皮"),
                    new(EraseAllButton, "清屏"),
                    new(UndoButton, "撤销"),
                    new(RedoButton, "重做"),
                    new(MoreToggle, "展开 / 收起"),
                ],
                [
                    new(CollapseExpandButton, "收起工具栏"),
                    new(MenuButton, "菜单"),
                    new(PrevPageButton, "上一页"),
                    new(AllPageToggle, "页码（点击预览）"),
                    new(NewNextPageButton, "下一页"),
                ],
                [
                    new(CopyButton, "复制"),
                    new(CutButton, "剪切"),
                    new(DeleteButton, "删除"),
                    new(CloneButton, "克隆（点击后在目标位置图章）"),
                ],
            ],
            [
                "单指绘图，两指平移缩放，三/四指平移",
                "五指及以上橡皮擦；手指间最大距离超过阈值时转多指书写",
                "已选中的按钮再次点击可取消",
            ]
        );
    }

    private void HelpClick(object o, RoutedEventArgs e)
    {
        var isChecked = HelpMenu.IsChecked;

        // 标注覆盖主工具栏两行，展开后才有上排按钮；关闭帮助时还原打开前的展开状态
        if (isChecked)
        {
            moreExpandedByHelp = MoreToggle.IsChecked != true;
            if (moreExpandedByHelp)
            {
                ApplyMoreToggle(true);
            }
        }
        else if (moreExpandedByHelp)
        {
            moreExpandedByHelp = false;
            if (MoreToggle.IsChecked == true)
            {
                ApplyMoreToggle(false);
            }
        }

        HelpLayer.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
    }
}
