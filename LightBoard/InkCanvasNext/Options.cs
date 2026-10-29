namespace InkCanvasNext;

/// <summary>
/// 画布构造期选项：创建时读入并写入实例的 readonly 字段，运行期不可变；改动需重建画布。
/// </summary>
public sealed record InkCanvasNextOptions
{
    /// <summary>
    /// 触点数量达到此值时掌心擦除，取值 4 或 5。
    /// </summary>
    public int PalmEraseFingers { get; init; } = InkCanvasNext.PalmEraseFingersDefault;
}
