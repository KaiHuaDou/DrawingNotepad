using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

using InkCanvasNext;

using Ookii.Dialogs.Wpf;

using static InkCanvasNext.InkCanvasNext;

namespace LightBoard;

public record LightBoardSettings(
    bool EdgeScrollHint = true,
    double EdgeScrollTopThreshold = EdgeScrollTopDefaultThreshold,
    double EdgeScrollBottomThreshold = EdgeScrollBottomDefaultThreshold,
    double EdgeScrollLeftThreshold = EdgeScrollLeftDefaultThreshold,
    double EdgeScrollRightThreshold = EdgeScrollRightDefaultThreshold,
    double TouchDisplacementThreshold = TouchDisplacementThresholdDefault,
    double PanZoomDisplaceThreshold = PanZoomDisplaceThresholdDefault,
    double PinchLockDistance = PinchLockDistanceDefault,
    int MaxHistoryCount = MaxHistoryCount,
    int PalmEraseFingers = PalmEraseFingersDefault,
    double EraserDiameter = EraserDiameterDefault,
    double EraserPalmExtra = EraserPalmExtraDefault,
    double EraserMinLogicalDiameter = EraserMinLogicalDiameterDefault,
    double EraserRebuildThreshold = EraserRebuildThresholdDefault
)
{
    /// <summary>
    /// 创建画布时读取的选项，改动需重启。
    /// </summary>
    [JsonIgnore]
    public InkCanvasNextOptions CanvasOptions => new( )
    {
        PalmEraseFingers = PalmEraseFingers,
        Eraser = new( )
        {
            Diameter = EraserDiameter,
            PalmExtra = EraserPalmExtra,
            MinLogicalDiameter = EraserMinLogicalDiameter,
            RebuildThreshold = EraserRebuildThreshold
        }
    };
}

public partial class Settings : Window
{
    private const double ThresholdMin = 1;
    private const double ThresholdMax = 200;
    private const double EdgeScrollMin = 0;
    private const double EdgeScrollMax = 1000;
    private const int HistoryMin = 10;
    private const int HistoryMax = 1000;
    private const double EraserDiameterMin = 1;
    private const double EraserDiameterMax = 400;
    private const double PalmExtraMin = 0;
    private const double PalmExtraMax = 100;
    private const double LogicalDiameterMin = 0.1;
    private const double LogicalDiameterMax = 10;
    private const double RebuildThresholdMin = 0.01;
    private const double RebuildThresholdMax = 10;

    private static readonly string SettingsJson = Path.Join(App.AppPath, "settings.json");
    private static readonly string SettingsJsonTemp = $"{SettingsJson}.tmp";

    public static LightBoardSettings Data { get; private set; } = new( );

    public Settings( )
    {
        InitializeComponent( );
        EdgeScrollHintBox.IsChecked = Data.EdgeScrollHint;
        EdgeScrollTopBox.Text = Data.EdgeScrollTopThreshold.ToString(CultureInfo.InvariantCulture);
        EdgeScrollBottomBox.Text = Data.EdgeScrollBottomThreshold.ToString(CultureInfo.InvariantCulture);
        EdgeScrollLeftBox.Text = Data.EdgeScrollLeftThreshold.ToString(CultureInfo.InvariantCulture);
        EdgeScrollRightBox.Text = Data.EdgeScrollRightThreshold.ToString(CultureInfo.InvariantCulture);
        TouchDisplacementBox.Text = Data.TouchDisplacementThreshold.ToString(CultureInfo.InvariantCulture);
        PanZoomDisplaceBox.Text = Data.PanZoomDisplaceThreshold.ToString(CultureInfo.InvariantCulture);
        PinchLockBox.Text = Data.PinchLockDistance.ToString(CultureInfo.InvariantCulture);
        MaxHistoryBox.Text = Data.MaxHistoryCount.ToString(CultureInfo.InvariantCulture);
        FourFingerEraserRadio.IsChecked = Data.PalmEraseFingers == PalmEraseFingersMin;
        FiveFingerEraserRadio.IsChecked = Data.PalmEraseFingers != PalmEraseFingersMin;
        EraserDiameterBox.Text = Data.EraserDiameter.ToString(CultureInfo.InvariantCulture);
        EraserPalmExtraBox.Text = Data.EraserPalmExtra.ToString(CultureInfo.InvariantCulture);
        EraserMinDiameterBox.Text = Data.EraserMinLogicalDiameter.ToString(CultureInfo.InvariantCulture);
        EraserRebuildBox.Text = Data.EraserRebuildThreshold.ToString(CultureInfo.InvariantCulture);
    }

    public static void LoadSettings( )
    {
        try
        {
            using var stream = new FileStream(SettingsJson, FileMode.Open, FileAccess.Read);
            Data = Normalize(JsonSerializer.Deserialize(stream, SettingsSerializerContext.Default.LightBoardSettings) ?? Data);
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

    /// <summary>
    /// 把手工改过的越界值收进可选范围：设置窗口与画布都按 Data 的值工作。
    /// </summary>
    internal static LightBoardSettings Normalize(LightBoardSettings data)
    {
        return data with
        {
            EdgeScrollTopThreshold = Math.Clamp(data.EdgeScrollTopThreshold, EdgeScrollMin, EdgeScrollMax),
            EdgeScrollBottomThreshold = Math.Clamp(data.EdgeScrollBottomThreshold, EdgeScrollMin, EdgeScrollMax),
            EdgeScrollLeftThreshold = Math.Clamp(data.EdgeScrollLeftThreshold, EdgeScrollMin, EdgeScrollMax),
            EdgeScrollRightThreshold = Math.Clamp(data.EdgeScrollRightThreshold, EdgeScrollMin, EdgeScrollMax),
            TouchDisplacementThreshold = Math.Clamp(data.TouchDisplacementThreshold, ThresholdMin, ThresholdMax),
            PanZoomDisplaceThreshold = Math.Clamp(data.PanZoomDisplaceThreshold, ThresholdMin, ThresholdMax),
            PinchLockDistance = Math.Clamp(data.PinchLockDistance, ThresholdMin, ThresholdMax),
            MaxHistoryCount = Math.Clamp(data.MaxHistoryCount, HistoryMin, HistoryMax),
            PalmEraseFingers = Math.Clamp(data.PalmEraseFingers, PalmEraseFingersMin, PalmEraseFingersMax),
            EraserDiameter = Math.Clamp(data.EraserDiameter, EraserDiameterMin, EraserDiameterMax),
            EraserPalmExtra = Math.Clamp(data.EraserPalmExtra, PalmExtraMin, PalmExtraMax),
            EraserMinLogicalDiameter = Math.Clamp(data.EraserMinLogicalDiameter, LogicalDiameterMin, LogicalDiameterMax),
            EraserRebuildThreshold = Math.Clamp(data.EraserRebuildThreshold, RebuildThresholdMin, RebuildThresholdMax)
        };
    }

    private void CancelClick(object o, RoutedEventArgs e)
    {
        Close( );
    }

    private void OkClick(object o, RoutedEventArgs e)
    {
        if (!TryReadRange(EdgeScrollTopBox.Text, EdgeScrollMin, EdgeScrollMax, out var edgeTop) ||
            !TryReadRange(EdgeScrollBottomBox.Text, EdgeScrollMin, EdgeScrollMax, out var edgeBottom) ||
            !TryReadRange(EdgeScrollLeftBox.Text, EdgeScrollMin, EdgeScrollMax, out var edgeLeft) ||
            !TryReadRange(EdgeScrollRightBox.Text, EdgeScrollMin, EdgeScrollMax, out var edgeRight) ||
            !TryReadRange(TouchDisplacementBox.Text, ThresholdMin, ThresholdMax, out var touch) ||
            !TryReadRange(PanZoomDisplaceBox.Text, ThresholdMin, ThresholdMax, out var panZoom) ||
            !TryReadRange(PinchLockBox.Text, ThresholdMin, ThresholdMax, out var pinch) ||
            !TryReadHistory(MaxHistoryBox.Text, out var maxHistory) ||
            !TryReadRange(EraserDiameterBox.Text, EraserDiameterMin, EraserDiameterMax, out var eraserDiameter) ||
            !TryReadRange(EraserPalmExtraBox.Text, PalmExtraMin, PalmExtraMax, out var palmExtra) ||
            !TryReadRange(EraserMinDiameterBox.Text, LogicalDiameterMin, LogicalDiameterMax, out var logicalDiameter) ||
            !TryReadRange(EraserRebuildBox.Text, RebuildThresholdMin, RebuildThresholdMax, out var rebuildThreshold))
        {
            ShowInvalidInput( );
            return;
        }

        var updated = Data with
        {
            EdgeScrollHint = EdgeScrollHintBox.IsChecked == true,
            EdgeScrollTopThreshold = edgeTop,
            EdgeScrollBottomThreshold = edgeBottom,
            EdgeScrollLeftThreshold = edgeLeft,
            EdgeScrollRightThreshold = edgeRight,
            TouchDisplacementThreshold = touch,
            PanZoomDisplaceThreshold = panZoom,
            PinchLockDistance = pinch,
            MaxHistoryCount = maxHistory,
            PalmEraseFingers = FourFingerEraserRadio.IsChecked == true ? PalmEraseFingersMin : PalmEraseFingersMax,
            EraserDiameter = eraserDiameter,
            EraserPalmExtra = palmExtra,
            EraserMinLogicalDiameter = logicalDiameter,
            EraserRebuildThreshold = rebuildThreshold
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

        var changed = updated != Data;
        Data = updated;

        if (changed)
        {
            ShowRestartNotice( );
        }

        Close( );
    }

    private static bool TryReadRange(string text, double min, double max, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && value >= min && value <= max;
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
            Content = $"""
                手势阈值需为 {ThresholdMin}~{ThresholdMax} 的视口 DIP 数值，撤销历史条数为 {HistoryMin}~{HistoryMax} 的整数；
                边缘热区宽度 {EdgeScrollMin}~{EdgeScrollMax}（视口 DIP，0 关闭该侧）；
                橡皮直径 {EraserDiameterMin}~{EraserDiameterMax}、掌擦外扩 {PalmExtraMin}~{PalmExtraMax}（视口 DIP）；
                最小逻辑直径 {LogicalDiameterMin}~{LogicalDiameterMax}、命中重建阈值 {RebuildThresholdMin}~{RebuildThresholdMax}（内容 DIP）。
                """,
        };
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }

    private static void ShowRestartNotice( )
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "设置已保存",
            MainIcon = TaskDialogIcon.Information,
            Content = "重启轻白板后按新设置运行。",
        };
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }
}

[JsonSourceGenerationOptions(AllowTrailingCommas = true, WriteIndented = true)]
[JsonSerializable(typeof(LightBoardSettings))]
public partial class SettingsSerializerContext : JsonSerializerContext;
