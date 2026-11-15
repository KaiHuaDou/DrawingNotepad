using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightBoard;

/// <summary>
/// 集合模式的数据集与选中项，保存在程序目录的 lottery.json
/// </summary>
internal static class LotteryStore
{
    internal static readonly string FilePath = Path.Join(App.AppPath, "lottery.json");

    internal static LotteryState Load( )
    {
        try
        {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read);

            return JsonSerializer.Deserialize(stream, LotterySerializerContext.Default.LotteryState) ?? new( );
        }
        catch (FileNotFoundException)
        {
            // 首次打开没有存档属正常
            return new( );
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            return new( );
        }
    }

    internal static void Save(LotteryState state)
    {
        try
        {
            using var stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(stream, state, LotterySerializerContext.Default.LotteryState);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }
}

// 抽选窗口的存档；缺字段的旧文件按这里的默认值生效
internal sealed class LotteryState
{
    public string? Selected { get; set; }

    public LotteryMode Mode { get; set; } = LotteryMode.Continuous;

    public int Max { get; set; } = 51;

    public int Lower { get; set; } = 1;

    public int Upper { get; set; } = 51;

    public int Count { get; set; } = 10;

    public List<LotteryStateDataset> Datasets { get; set; } = [];
}

internal sealed class LotteryStateDataset
{
    public string Name { get; set; } = string.Empty;

    public List<string> Items { get; set; } = [];
}

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(LotteryState))]
internal sealed partial class LotterySerializerContext : JsonSerializerContext;
