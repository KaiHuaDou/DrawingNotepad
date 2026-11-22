using System;

using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace InkCanvasNext;

/// <summary>
/// 把手写笔的设备压力反解成 WPF 压力，使最终宽度等于笔宽乘设备压力
/// 反解公式：p' = (p - 0.25) / 1.5；关闭设备压力时固定为 0.5
/// </summary>
internal sealed class DevicePressureStylusPlugIn(InputArbiter arbiter, PenParameters parameters) : StylusPlugIn
{
    protected override void OnStylusDown(RawStylusInput input)
    {
        Remap(input);
    }

    protected override void OnStylusMove(RawStylusInput input)
    {
        Remap(input);
    }

    private void Remap(RawStylusInput input)
    {
        // 只改写确认是手写笔的设备：触摸提升为笔报文时设备号同样非 0，
        // 设备表未枚举完就按笔处理会把触摸压力 0.5 改成 0.1667，宽度减半
        if (!arbiter.IsStylusTabletId(input.TabletDeviceId))
        {
            return;
        }

        // 注意：RawStylusInput.GetStylusPoints() 每次都会从原始报文新建一份点集合，
        // SetStylusPoints() 内部还会再 Clone 一次；因此每个笔事件至少发生两次点集合复制，
        // 这是 WPF 公共 API 的固定成本，不是本方法额外制造的副本
        var points = input.GetStylusPoints( );
        var changed = false;

        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var pressure = Map(point.PressureFactor, parameters.UseDevicePressure);

            if (pressure == point.PressureFactor)
            {
                continue;
            }

            point.PressureFactor = pressure;
            points[i] = point;
            changed = true;
        }

        if (changed)
        {
            input.SetStylusPoints(points);
        }
    }

    /// <summary>
    /// 把设备压力反解成 WPF 压力；关闭设备压力时固定 0.5
    /// </summary>
    internal static float Map(float pressure, bool useDevicePressure)
    {
        var target = useDevicePressure
            ? (pressure - 0.25f) / 1.5f
            : 0.5f;

        return Math.Clamp(target, 0, 1);
    }
}
