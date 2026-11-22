using System.Collections.Generic;
using System.Threading.Tasks;

namespace LightBoard;

/// <summary>
/// .lbf 与板状态之间的桥接：读盘、注册背景来源，再替换或追加页
/// </summary>
internal static class BoardPersistence
{
    public static async Task<IReadOnlyList<string>> LoadAsync(string path)
    {
        var content = Board.Read(path);

        // 背景服务先注册完再动 Pages：打开在 await 处提前返回时，Pages 不能停在半空状态。
        await DocumentRegistry.RegisterBackgroundsAsync(content.Pages);

        BoardState.Load(content.Pages, content.Underlay);

        return content.InvalidBackgrounds;
    }

    public static async Task<IReadOnlyList<string>> AttachAsync(string path)
    {
        var content = Board.Read(path);
        await DocumentRegistry.RegisterBackgroundsAsync(content.Pages);

        BoardState.Append(content.Pages);

        return content.InvalidBackgrounds;
    }
}
