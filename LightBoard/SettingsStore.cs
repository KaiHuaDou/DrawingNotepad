using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LightBoard;

/// <summary>
/// settings.json 的读写；<see cref="Current"/> 是下次启动要用的值，运行期不在中途重新读取
/// </summary>
public static class SettingsStore
{
    private static readonly string SettingsJson = Path.Join(App.AppPath, "settings.json");
    private static readonly string SettingsJsonTemp = $"{SettingsJson}.tmp";

    public static LightBoardSettings Current { get; private set; } = new( );

    public static void Load( )
    {
        try
        {
            using var stream = new FileStream(SettingsJson, FileMode.Open, FileAccess.Read);
            Current = LightBoardSettings.Normalize(JsonSerializer.Deserialize(stream, SettingsSerializerContext.Default.LightBoardSettings) ?? Current);
        }
        catch (FileNotFoundException)
        {
            // 首次运行没有设置文件属正常
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }

    public static void Save(LightBoardSettings settings)
    {
        using (var stream = new FileStream(SettingsJsonTemp, FileMode.Create, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, settings, SettingsSerializerContext.Default.LightBoardSettings);
        }

        File.Move(SettingsJsonTemp, SettingsJson, true);
        Current = settings;
    }
}

[JsonSourceGenerationOptions(AllowTrailingCommas = true, WriteIndented = true)]
[JsonSerializable(typeof(LightBoardSettings))]
public partial class SettingsSerializerContext : JsonSerializerContext;
