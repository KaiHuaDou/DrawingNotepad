using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

public record LightBoardSettings(
    bool AutoScroll = true,
    double TouchDisplacementThreshold = InkCanvasNext.InkCanvasNext.TouchDisplacementThresholdDefault,
    double PanZoomDisplaceThreshold = InkCanvasNext.InkCanvasNext.PanZoomDisplaceThresholdDefault,
    double PinchLockDistance = InkCanvasNext.InkCanvasNext.PinchLockDistanceDefault,
    int MaxHistoryCount = InkCanvasNext.InkCanvasNext.MaxHistoryCount
);

public partial class Settings : Window
{
    public const double ThresholdMin = 1;
    public const double ThresholdMax = 200;
    public const int HistoryMin = 10;
    public const int HistoryMax = 1000;

    private static readonly string SettingsJson = Path.Join(App.AppPath, "settings.json");
    private static readonly string SettingsJsonTemp = $"{SettingsJson}.tmp";

    public static LightBoardSettings Data { get; private set; } = new( );

    public Settings( )
    {
        InitializeComponent( );
        AutoScrollBox.IsChecked = Data.AutoScroll;
        TouchDisplacementBox.Text = Data.TouchDisplacementThreshold.ToString(CultureInfo.InvariantCulture);
        PanZoomDisplaceBox.Text = Data.PanZoomDisplaceThreshold.ToString(CultureInfo.InvariantCulture);
        PinchLockBox.Text = Data.PinchLockDistance.ToString(CultureInfo.InvariantCulture);
        MaxHistoryBox.Text = Data.MaxHistoryCount.ToString(CultureInfo.InvariantCulture);
    }

    public static void LoadSettings( )
    {
        try
        {
            using var stream = new FileStream(SettingsJson, FileMode.Open, FileAccess.Read);
            Data = JsonSerializer.Deserialize(stream, SettingsSerializerContext.Default.LightBoardSettings) ?? Data;
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

    private void CancelClick(object o, RoutedEventArgs e)
    {
        Close( );
    }

    private void OkClick(object o, RoutedEventArgs e)
    {
        if (!TryReadThreshold(TouchDisplacementBox.Text, out var touch) ||
            !TryReadThreshold(PanZoomDisplaceBox.Text, out var panZoom) ||
            !TryReadThreshold(PinchLockBox.Text, out var pinch) ||
            !TryReadHistory(MaxHistoryBox.Text, out var maxHistory))
        {
            ShowInvalidInput( );
            return;
        }

        var updated = Data with
        {
            AutoScroll = AutoScrollBox.IsChecked == true,
            TouchDisplacementThreshold = touch,
            PanZoomDisplaceThreshold = panZoom,
            PinchLockDistance = pinch,
            MaxHistoryCount = maxHistory
        };
        try
        {
            using (var stream = new FileStream(SettingsJsonTemp, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, updated, SettingsSerializerContext.Default.LightBoardSettings);
            }

            File.Move(SettingsJsonTemp, SettingsJson, true);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            App.ShowException(ex, "无法写入设置");
            return;
        }

        Data = updated;
        Close( );
    }

    private static bool TryReadThreshold(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value >= ThresholdMin && value <= ThresholdMax;
    }

    private static bool TryReadHistory(string text, out int value)
    {
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               && value >= HistoryMin && value <= HistoryMax;
    }

    private static void ShowInvalidInput( )
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "参数输入无效",
            MainIcon = TaskDialogIcon.Warning,
            Content = $"三个手势阈值需为 {ThresholdMin}~{ThresholdMax} 的数字（视口 DIP），撤销历史条数需为 {HistoryMin}~{HistoryMax} 的整数。",
        };
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }
}

[JsonSourceGenerationOptions(AllowTrailingCommas = true, WriteIndented = true)]
[JsonSerializable(typeof(LightBoardSettings))]
public partial class SettingsSerializerContext : JsonSerializerContext;
