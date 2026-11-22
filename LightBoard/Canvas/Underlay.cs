using System.Windows.Media;

namespace LightBoard;

// 板级底色：垫在墨迹下的画布底衬，网格、纯白、纯黑三选一
internal enum Underlay
{
    Grid,
    White,
    Black,
}

internal static class UnderlayExtensions
{
    /// <summary>
    /// 底色对应的颜色；网格返回 null，表示沿用画布与导出各自的默认底纹
    /// </summary>
    public static Color? ToColor(this Underlay underlay)
    {
        return underlay switch
        {
            Underlay.White => Colors.White,
            Underlay.Black => Colors.Black,
            _ => null,
        };
    }
}
