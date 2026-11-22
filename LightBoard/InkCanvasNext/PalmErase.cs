using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasNext;

/// <summary>
/// 手掌面积通道：按下时按触点尺寸给 <see cref="TouchContact.Kind"/> 分类，
/// 手掌触点仍留在同一触点集合中，手势判定只读手指，擦除几何只读手掌。
/// 尺寸不可用的设备（0 或 1×1）不会产生手掌触点，全部路径退化为 4/5 指擦除
/// </summary>
public partial class InkCanvasNext
{
    private const double PalmContactSizeMin = 20;
    private const double PalmContactSizeMax = 2000;

    // 以最后落下的手掌触点为种子的连通簇：同一次掌压的碎片互相连通，另一只手掌不参与几何
    private readonly List<TouchContact> palmCluster = [];

    // 当前擦除周期是否已改用面积几何；由 EndEraserCycle 统一清零，手掌簇组成变化时重启
    private bool areaEraseCycle;

    // 当前面积周期对应的手掌簇签名；成员变化时在新位置重建命中测试器，避免沿旧中心到新中心扫过
    private int areaClusterSignature;

    // Eraser 生命周期是否用过面积通道：手掌抬走后剩余手指停止擦除
    private bool areaEraseUsed;
    private long areaCycleStart;
    private double frozenPalmDiameter;
    private double palmScreenWidthPx;

    /// <summary>
    /// 触点尺寸是否落在有效范围：过小视为噪声，过大视为单位异常
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsUsableContactSize(Size size)
    {
        return size.Width >= PalmContactSizeMin
            && size.Width <= PalmContactSizeMax
            && size.Height >= PalmContactSizeMin
            && size.Height <= PalmContactSizeMax;
    }

    /// <summary>
    /// 手掌判据按长短边归一化，两个方向都严格大于阈值
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsPalmSize(Size size)
    {
        return Math.Max(size.Width, size.Height) > options.PalmMinWidth
            && Math.Min(size.Width, size.Height) > options.PalmMinHeight;
    }

    /// <summary>
    /// 新触点的分类：单触点达标、靠近已有手掌触点、或与附近手指面积和超过阈值
    /// </summary>
    private bool ShouldTreatAsPalm(Point position, Size size)
    {
        if (Snapshot.IsReadOnly || !IsUsableContactSize(size))
        {
            return false;
        }

        if (IsPalmSize(size))
        {
            return true;
        }

        foreach (var contact in touches.Values)
        {
            if (!contact.IsPalm)
            {
                continue;
            }

            if (Geometry.Distance2(contact.Position, position) <= options.PalmMergeDistance2)
            {
                return true;
            }
        }

        return TryClusterToPalm(position, size);
    }

    /// <summary>
    /// 手掌被拆成多个小触点时按面积求和判定，命中后把候选触点改为手掌；
    /// 没有候选触点时不走面积通道，避免单个触点绕过长短边判据
    /// </summary>
    private bool TryClusterToPalm(Point position, Size size)
    {
        Span<int> ids = stackalloc int[touches.Count];
        var count = 0;
        var area = size.Width * size.Height;

        foreach (var (id, contact) in touches)
        {
            if (contact.IsPalm
                || !IsUsableContactSize(contact.Size)
                || Geometry.Distance2(contact.Position, position) > options.PalmMergeDistance2)
            {
                continue;
            }

            ids[count++] = id;
            area += contact.Size.Width * contact.Size.Height;
        }

        if (count == 0 || area <= options.PalmClusterMinArea)
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            MarkTouchAsPalm(ids[i]);
        }

        RefreshGestureAfterPalmMove( );
        return true;
    }

    /// <summary>
    /// 手指转为手掌：放弃其多指笔画、移出位移跟踪；若它正驱动选区则交给剩余手指
    /// </summary>
    private void MarkTouchAsPalm(int id)
    {
        if (!touches.TryGetValue(id, out var contact) || contact.IsPalm)
        {
            return;
        }

        CancelMultiTouchStroke(id);
        touches[id] = contact with { Kind = TouchKind.Palm };
        touchStarts.Remove(id);

        if (SelectionDriverId == id)
        {
            SelectionDriverId = null;
            RebaseSelectionDriver( );
        }
    }

    /// <summary>
    /// 触点集合变化会换掉手势基线，迁出手掌触点后按当前状态重建
    /// </summary>
    private void RefreshGestureAfterPalmMove( )
    {
        if (State is TouchState.EvalPan or TouchState.Pan or TouchState.PanZoom)
        {
            InitGesture( );
        }
        else if (State == TouchState.Selection)
        {
            RefreshSelectionGesture( );
        }
    }

    /// <summary>
    /// 手掌接管判定：集中一处，状态机的 switch 只保留纯手指数逻辑。
    /// 评估期与空闲期立即接管；已提交的手势与绘制只在手指清空后接管；选区要求全部触点在选区外
    /// </summary>
    private bool IsPalmTakeover(TouchState state, int fingerCount, int palmCount)
    {
        if (palmCount == 0 || Snapshot.IsReadOnly)
        {
            return false;
        }

        return state switch
        {
            TouchState.Idle or TouchState.EvalDraw or TouchState.EvalPan => true,
            TouchState.Draw or TouchState.MultiDraw or TouchState.PanZoom or TouchState.Pan
                => fingerCount == 0,
            TouchState.Selection => AllContactsOutsideSelection( ),
            _ => false,
        };
    }

    /// <summary>
    /// 以最后落下的手掌触点为种子求连通簇：两掌相距较远时橡皮跟随新落下的手掌，
    /// 不会被拉到两掌中间，也不会按两只手掌的并集放大直径
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void BuildPalmCluster( )
    {
        palmCluster.Clear( );

        TouchContact? seed = null;
        foreach (var contact in touches.Values)
        {
            if (contact.IsPalm)
            {
                seed = contact;
            }
        }

        if (seed is null)
        {
            return;
        }

        palmCluster.Add(seed.Value);

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var contact in touches.Values)
            {
                if (!contact.IsPalm || ClusterContains(contact.Device))
                {
                    continue;
                }

                foreach (var member in palmCluster)
                {
                    if (Geometry.Distance2(member.Position, contact.Position) > options.PalmMergeDistance2)
                    {
                        continue;
                    }

                    palmCluster.Add(contact);
                    changed = true;
                    break;
                }
            }
        }
    }

    private bool ClusterContains(TouchDevice device)
    {
        foreach (var contact in palmCluster)
        {
            if (contact.Device == device)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 手掌几何一次算全：生产路径与调试快照共用，避免公式分叉
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private PalmGeometry GetPalmGeometry( )
    {
        BuildPalmCluster( );
        if (palmCluster.Count == 0)
        {
            return default;
        }

        var (width, height) = GetPalmBox(palmCluster);
        var diagonal = Math.Sqrt(width * width + height * height);
        var ratio = PalmRatio(diagonal);
        if (PalmScreenWidth( ) > 3000)
        {
            ratio *= 0.8;
        }

        var (center, radius) = GetClusterEnvelope(palmCluster);

        // 质心是仿射不变量：视口质心做一次坐标变换即可得到内容质心，
        // 不必逐触点调用 GetTouchPoint
        var canvasCenter = TranslatePoint(center, InnerCanvas);
        var area = options.Eraser.PalmBaseDiameter * ratio;
        var envelope = radius * 2 + options.Eraser.PalmExtra;
        var raw = Math.Clamp(
            Math.Max(area, envelope),
            options.Eraser.PalmMinDiameter,
            EraserParameters.DiameterMax);

        return new(center, canvasCenter, width, height, diagonal, area, envelope, raw, GetClusterSignature(palmCluster));
    }

    /// <summary>
    /// 面积直径与包络直径取较大者，再夹在最小直径与橡皮上限之间；
    /// 周期开始后的调整窗口内可按面积调整，此后固定
    /// </summary>
    private double ResolvePalmDiameter(in PalmGeometry geometry)
    {
        var adjusting = Stopwatch.GetElapsedTime(areaCycleStart).TotalMilliseconds
            <= options.Eraser.PalmSizeAdjustDuration;

        if (adjusting || frozenPalmDiameter <= 0)
        {
            frozenPalmDiameter = geometry.RawDiameter;
        }

        return frozenPalmDiameter;
    }

    /// <summary>
    /// 开始或重启面积擦除周期：重置冻结直径，并从当前时刻重开调整窗口
    /// </summary>
    private void BeginAreaEraseCycle( )
    {
        areaEraseCycle = true;
        areaEraseUsed = true;
        areaCycleStart = Stopwatch.GetTimestamp( );
        frozenPalmDiameter = 0;
    }

    private void EndAreaEraseCycle( )
    {
        areaEraseCycle = false;
        areaClusterSignature = 0;
        frozenPalmDiameter = 0;
    }

    /// <summary>
    /// 进入 Eraser 时按当时的手掌集合开启生命周期：有手掌走面积周期，否则留给手指数通道
    /// </summary>
    private void BeginPalmEraseLifetime( )
    {
        areaEraseUsed = CountTouches(touches).Palms > 0;

        if (areaEraseUsed)
        {
            BeginAreaEraseCycle( );
        }
        else
        {
            EndAreaEraseCycle( );
        }
    }

    /// <summary>
    /// 结束一次 Eraser 生命周期：不可再由剩余手指续擦，下一次进入 Eraser 重新判定
    /// </summary>
    private void ResetPalmEraseLifetime( )
    {
        areaEraseUsed = false;
        EndAreaEraseCycle( );
    }

    private double PalmScreenWidth( )
    {
        if (palmScreenWidthPx <= 0)
        {
            palmScreenWidthPx = SystemParameters.PrimaryScreenWidth * VisualTreeHelper.GetDpi(this).DpiScaleX;
        }

        return palmScreenWidthPx;
    }

    private void ResetPalmState( )
    {
        palmCluster.Clear( );
        ResetPalmEraseLifetime( );
    }
}
