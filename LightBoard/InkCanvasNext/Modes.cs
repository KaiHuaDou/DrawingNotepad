namespace InkCanvasNext;

/// <summary>
/// 画布编辑工具模式
/// </summary>
public enum InkCanvasNextMode
{
    /// <summary>
    /// 使用默认笔触绘制墨迹
    /// </summary>
    Ink,
    /// <summary>
    /// 按笔画擦除墨迹
    /// </summary>
    EraseStroke,
    /// <summary>
    /// 按区域擦除墨迹
    /// </summary>
    EraseArea,
    /// <summary>
    /// 选择墨迹笔画
    /// </summary>
    Select,
    /// <summary>
    /// 绘制直线
    /// </summary>
    Line,
    /// <summary>
    /// 绘制圆形
    /// </summary>
    Circle
}

/// <summary>
/// 盖章动作（克隆选区或粘贴剪贴板墨迹）
/// </summary>
public enum StampAction
{
    /// <summary>
    /// 无盖章动作
    /// </summary>
    None,
    /// <summary>
    /// 克隆选区墨迹
    /// </summary>
    Clone,
    /// <summary>
    /// 粘贴剪贴板中的墨迹
    /// </summary>
    Paste
}

/// <summary>
/// 鼠标滚轮动作模式
/// </summary>
public enum MouseWheelAction
{
    /// <summary>
    /// 滚动画布
    /// </summary>
    Scroll,
    /// <summary>
    /// 缩放画布
    /// </summary>
    Zoom,
    /// <summary>
    /// 忽略鼠标滚轮
    /// </summary>
    None
}
