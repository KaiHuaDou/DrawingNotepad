namespace LightBoard;

// 板级底色：垫在墨迹下的画布底衬，网格、纯白、纯黑三选一
internal enum Underlay
{
    Grid,
    White,
    Black,
}

public partial class App
{
    /// <summary>
    /// 当前板子的底色；写进 .lbf，随文件一起保存
    /// </summary>
    internal static Underlay Underlay { get; set; }
}
