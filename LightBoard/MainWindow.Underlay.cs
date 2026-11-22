using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightBoard;

public partial class MainWindow
{
    private static readonly Brush MajorGridBrush = (Application.Current.FindResource("MajorGridBrush") as Brush)!;

    /// <summary>
    /// 底色单选的唯一解码点：画布底衬按这里的结果分派
    /// </summary>
    private Underlay CurrentUnderlay( )
    {
        return WhiteUnderlayRadio.IsChecked == true ? Underlay.White
            : BlackUnderlayRadio.IsChecked == true ? Underlay.Black
            : Underlay.Grid;
    }

    /// <summary>
    /// 板子的底色写回单选：打开 .lbf 后按记录选中对应项
    /// </summary>
    internal void SyncUnderlay( )
    {
        (BoardState.Underlay switch
        {
            Underlay.White => WhiteUnderlayRadio,
            Underlay.Black => BlackUnderlayRadio,
            _ => DefaultUnderlayRadio,
        }).IsChecked = true;

        ApplyAppearance( );
    }

    private static Brush UnderlayBrush(Underlay underlay)
    {
        return underlay switch
        {
            Underlay.White => Brushes.White,
            Underlay.Black => Brushes.Black,
            _ => MajorGridBrush,
        };
    }

    private void UnderlayRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { IsChecked: true })
        {
            return;
        }

        ApplyAppearance( );

        // 缩略图按底色重画
        foreach (var page in BoardState.Pages)
        {
            page.InvalidatePreview( );
        }
    }
}
