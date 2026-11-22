namespace LightBoard;

/// <summary>
/// 拍照后照片落在原页还是新页
/// </summary>
internal enum PhotoPlacement
{
    OriginalPage,
    NewPage,
}

internal static class PhotoPlacementPolicy
{
    /// <summary>
    /// 原页没有背景时照片留在原页，否则另开一页追加到最后
    /// </summary>
    public static PhotoPlacement For(Page page)
    {
        return page.Background is null ? PhotoPlacement.OriginalPage : PhotoPlacement.NewPage;
    }
}
