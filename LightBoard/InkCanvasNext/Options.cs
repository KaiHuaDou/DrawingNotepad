using System;

using System.Windows;

namespace InkCanvasNext;

/// <summary>
/// InkCanvasNext 选项：运行期不可变；改动需重建画布。
/// 各属性在 init 时收拢到有效范围，越界值取边界
/// </summary>
public sealed record InkCanvasNextOptions
{
    /// <summary>
    /// 触点数量达到此值时掌擦，取值 4 或 5
    /// </summary>
    public int PalmEraseFingers
    {
        get;
        init => field = Math.Clamp(value, PalmEraseFingersMin, PalmEraseFingersMax);
    } = PalmEraseFingersDefault;

    /// <summary>
    /// 单指起笔位移阈值，视口 DIP
    /// </summary>
    public double TouchDisplacementThreshold
    {
        get;
        init
        {
            field = ClampGesture(value);
            TouchDisplacementThreshold2 = field * field;
        }
    } = TouchDisplacementThresholdDefault;

    /// <summary>
    /// <see cref="TouchDisplacementThreshold"/> 的平方，逐事件判定的热点路径预计算
    /// </summary>
    internal double TouchDisplacementThreshold2
    {
        get;
        private init;
    } = TouchDisplacementThresholdDefault * TouchDisplacementThresholdDefault;

    /// <summary>
    /// 双指平移锁定阈值，视口 DIP
    /// </summary>
    public double PanZoomDisplaceThreshold
    {
        get;
        init
        {
            field = ClampGesture(value);
            PanZoomDisplaceThreshold2 = field * field;
        }
    } = PanZoomDisplaceThresholdDefault;

    /// <summary>
    /// <see cref="PanZoomDisplaceThreshold"/> 的平方，逐事件判定的热点路径预计算
    /// </summary>
    internal double PanZoomDisplaceThreshold2
    {
        get;
        private init;
    } = PanZoomDisplaceThresholdDefault * PanZoomDisplaceThresholdDefault;

    /// <summary>
    /// 双指捏合锁定间距，视口 DIP
    /// </summary>
    public double PinchLockDistance
    {
        get;
        init
        {
            field = ClampGesture(value);
            PinchLockDistance2 = field * field;
        }
    } = PinchLockDistanceDefault;

    /// <summary>
    /// <see cref="PinchLockDistance"/> 的平方，逐事件判定的热点路径预计算
    /// </summary>
    internal double PinchLockDistance2
    {
        get;
        private init;
    } = PinchLockDistanceDefault * PinchLockDistanceDefault;

    /// <summary>
    /// 触点间距阈值，视口 DIP；赋非正值或非有限值时按主屏工作区宽度派生
    /// </summary>
    public double TouchSpacingThreshold
    {
        get;
        init
        {
            field = double.IsFinite(value) && value > 0 ? value : DefaultSpacing;
            TouchSpacingThreshold2 = field * field;
        }
    } = DefaultSpacing;

    /// <summary>
    /// <see cref="TouchSpacingThreshold"/> 的平方，逐事件判定的热点路径预计算
    /// </summary>
    internal double TouchSpacingThreshold2
    {
        get;
        private init;
    } = DefaultSpacing * DefaultSpacing;

    /// <summary>
    /// 上侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollTopThreshold
    {
        get;
        init => field = ClampEdge(value);
    } = EdgeScrollTopDefault;

    /// <summary>
    /// 下侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollBottomThreshold
    {
        get;
        init => field = ClampEdge(value);
    } = EdgeScrollBottomDefault;

    /// <summary>
    /// 左侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollLeftThreshold
    {
        get;
        init => field = ClampEdge(value);
    } = EdgeScrollLeftDefault;

    /// <summary>
    /// 右侧热区宽度，视口 DIP；0 关闭该侧提示
    /// </summary>
    public double EdgeScrollRightThreshold
    {
        get;
        init => field = ClampEdge(value);
    } = EdgeScrollRightDefault;

    /// <summary>
    /// 撤销历史容量；0 表示不记录历史
    /// </summary>
    public int MaxHistoryCount
    {
        get;
        init => field = Math.Clamp(value, MaxHistoryCountMin, MaxHistoryCountMax);
    } = MaxHistoryCountDefault;

    /// <summary>
    /// 鼠标滚轮行为；赋值超出枚举范围时取 Scroll
    /// </summary>
    public MouseWheelAction WheelAction
    {
        get;
        init => field = Enum.IsDefined(value) ? value : MouseWheelAction.Scroll;
    } = MouseWheelAction.Scroll;

    /// <summary>
    /// 橡皮参数
    /// </summary>
    public EraserParameters Eraser
    {
        get;
        init => field = value ?? new( );
    } = new( );

    public const int PalmEraseFingersMin = 4;
    public const int PalmEraseFingersMax = 5;
    public const int PalmEraseFingersDefault = PalmEraseFingersMax;

    /// <summary>
    /// 手势类阈值的共同范围，视口 DIP
    /// </summary>
    public const double GestureMin = 1;
    public const double GestureMax = 200;

    public const double TouchDisplacementThresholdDefault = 20;
    public const double PanZoomDisplaceThresholdDefault = 30;
    public const double PinchLockDistanceDefault = 24;

#if DEBUG
    public const double DistanceThresholdFactor = 0.9;
#else
    public const double DistanceThresholdFactor = 0.1;
#endif

    /// <summary>
    /// 边缘热区宽度的范围，视口 DIP
    /// </summary>
    public const double EdgeScrollThresholdMin = 0;
    public const double EdgeScrollThresholdMax = 1000;

    public const double EdgeScrollTopDefault = 64;
    public const double EdgeScrollBottomDefault = 192;
    public const double EdgeScrollLeftDefault = 64;
    public const double EdgeScrollRightDefault = 96;

    public const int MaxHistoryCountDefault = 200;
    public const int MaxHistoryCountMin = 0;
    public const int MaxHistoryCountMax = 1000;

    private static double DefaultSpacing => DistanceThresholdFactor * SystemParameters.WorkArea.Width;

    private static double ClampGesture(double value)
    {
        return double.IsFinite(value) ? Math.Clamp(value, GestureMin, GestureMax) : GestureMin;
    }

    private static double ClampEdge(double value)
    {
        return double.IsFinite(value) ? Math.Clamp(value, EdgeScrollThresholdMin, EdgeScrollThresholdMax) : EdgeScrollThresholdMin;
    }
}

/// <summary>
/// 橡皮参数
/// </summary>
public sealed record EraserParameters
{
    /// <summary>
    /// 面积擦直径，视口 DIP
    /// </summary>
    public double Diameter
    {
        get;
        init => field = Math.Clamp(value, DiameterMin, DiameterMax);
    } = DiameterDefault;

    /// <summary>
    /// 掌擦圆在触点包络直径之外的外扩量，视口 DIP
    /// </summary>
    public double PalmExtra
    {
        get;
        init => field = Math.Clamp(value, PalmExtraMin, PalmExtraMax);
    } = PalmExtraDefault;

    /// <summary>
    /// 缩放后逻辑直径的下限，内容 DIP
    /// </summary>
    public double MinLogicalDiameter
    {
        get;
        init => field = Math.Clamp(value, MinLogicalDiameterMin, MinLogicalDiameterMax);
    } = MinLogicalDiameterDefault;

    /// <summary>
    /// 逻辑直径变化超过此值才重建命中测试器，内容 DIP
    /// </summary>
    public double RebuildThreshold
    {
        get;
        init => field = Math.Clamp(value, RebuildThresholdMin, RebuildThresholdMax);
    } = RebuildThresholdDefault;

    public const double DiameterDefault = 75;
    public const double DiameterMin = 1;
    public const double DiameterMax = 400;

    public const double PalmExtraDefault = 8;
    public const double PalmExtraMin = 0;
    public const double PalmExtraMax = 100;

    public const double MinLogicalDiameterDefault = 1;
    public const double MinLogicalDiameterMin = 0.1;
    public const double MinLogicalDiameterMax = 10;

    public const double RebuildThresholdDefault = 0.5;
    public const double RebuildThresholdMin = 0.01;
    public const double RebuildThresholdMax = 10;
}
