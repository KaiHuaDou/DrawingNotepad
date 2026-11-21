using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace InkCanvasNext;

internal enum InputKind
{
    Stylus = 0,
    Mouse = 1,
    Touch = 2
}

/// <summary>
/// 输入优先级：触摸高于鼠标，鼠标高于手写笔
/// 被拒绝的输入仍记录按下，直到抬起前保持拒绝
/// </summary>
internal sealed class InputArbiter
{
    private readonly HashSet<int> touchIds = [];
    private HashSet<int> touchTabletIds = [];
    private HashSet<int> stylusTabletIds = [];

    private volatile int touchCount;
    private volatile bool mouseDown;
    private volatile bool mouseAccepted;
    private volatile bool penDown;
    private volatile bool penAccepted;

    internal int TouchCount => touchCount;

    internal bool MouseDown => mouseDown;

    internal bool PenDown => penDown;

    internal bool MouseAccepted => mouseDown && mouseAccepted;

    internal bool PenAccepted => penDown && penAccepted;

    /// <summary>
    /// 最近一次被接受的输入，供笔画收集时判断是否应用笔锋
    /// </summary>
    internal InputKind? StrokeKind { get; private set; }

    internal bool Accepts(InputKind kind)
    {
        return kind switch
        {
            InputKind.Touch => true,
            InputKind.Mouse => touchCount == 0 && !mouseDown,
            InputKind.Stylus => touchCount == 0 && !mouseDown && !penDown,
            _ => false
        };
    }

    internal bool Begin(InputKind kind, int id = 0)
    {
        var accepted = Accepts(kind);

        switch (kind)
        {
            case InputKind.Touch:
                touchIds.Add(id);
                touchCount = touchIds.Count;
                StrokeKind = InputKind.Touch;
                break;

            case InputKind.Mouse:
                mouseDown = true;
                mouseAccepted = accepted;
                if (accepted)
                {
                    StrokeKind = InputKind.Mouse;
                }

                break;

            case InputKind.Stylus:
                penDown = true;
                penAccepted = accepted;
                if (accepted)
                {
                    StrokeKind = InputKind.Stylus;
                }

                break;
        }

        return accepted;
    }

    internal void End(InputKind kind, int id = 0)
    {
        switch (kind)
        {
            case InputKind.Touch:
                touchIds.Remove(id);
                touchCount = touchIds.Count;
                break;

            case InputKind.Mouse:
                mouseDown = false;
                mouseAccepted = false;
                break;

            case InputKind.Stylus:
                penDown = false;
                penAccepted = false;
                break;
        }
    }

    internal void Reset( )
    {
        touchIds.Clear( );
        touchCount = 0;
        mouseDown = false;
        mouseAccepted = false;
        penDown = false;
        penAccepted = false;
        StrokeKind = null;
    }

    /// <summary>
    /// 渲染器线程使用：低优先级输入不进入湿墨
    /// </summary>
    internal bool Allows(InputKind kind)
    {
        return kind switch
        {
            InputKind.Touch => true,
            InputKind.Mouse => touchCount == 0 && (!mouseDown || mouseAccepted),
            InputKind.Stylus => touchCount == 0 && !mouseDown && (!penDown || penAccepted),
            _ => false
        };
    }

    /// <summary>
    /// 手写笔设备分类；null 表示鼠标，DynamicRenderer.Reset 会以 null 传入鼠标
    /// </summary>
    internal static InputKind KindOf(StylusDevice? device)
    {
        if (device is null)
        {
            return InputKind.Mouse;
        }

        if (device.TabletDevice.Type == TabletDeviceType.Touch)
        {
            return InputKind.Touch;
        }

        return device.Id == 0 || device.TabletDevice.Id == 0
            ? InputKind.Mouse
            : InputKind.Stylus;
    }

    internal InputKind KindOf(RawStylusInput input)
    {
        if (IsTouchTabletId(input.TabletDeviceId))
        {
            return InputKind.Touch;
        }

        return input.StylusDeviceId == 0 || input.TabletDeviceId == 0
            ? InputKind.Mouse
            : InputKind.Stylus;
    }

    /// <summary>
    /// 渲染器线程使用：平板设备号是否属于触摸设备
    /// 触摸提升为笔报文时设备号同样非 0，不能用 0 号区分；设备表未枚举时按非触摸处理
    /// </summary>
    internal bool IsTouchTabletId(int id)
    {
        return id != 0 && touchTabletIds.Contains(id);
    }

    /// <summary>
    /// 渲染器线程使用：只有确认属于手写笔的平板设备才改写压力
    /// 设备表未枚举完就按笔处理会把压力 0.5 的触摸改成 0.1667，触摸宽度减半
    /// </summary>
    internal bool IsStylusTabletId(int id)
    {
        return id != 0 && stylusTabletIds.Contains(id);
    }

    internal void RefreshTabletDevices( )
    {
        var touchIds = new HashSet<int>( );
        var stylusIds = new HashSet<int>( );
        var devices = Tablet.TabletDevices;

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];

            if (device.Type == TabletDeviceType.Touch)
            {
                touchIds.Add(device.Id);
            }
            else
            {
                stylusIds.Add(device.Id);
            }
        }

        touchTabletIds = touchIds;
        stylusTabletIds = stylusIds;
    }
}
