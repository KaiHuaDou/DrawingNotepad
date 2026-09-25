using System;
using System.Windows.Media;

namespace LightBoard;

/// <summary>
/// 帧率表：订阅 CompositionTarget.Rendering，按时间窗统计帧率与最长帧。
/// 订阅期间 WPF 会保持逐帧的渲染节拍（静止内容约 30 ms 一帧），因此只在需要读数期间 Start。
/// 帧间隔取 RenderingTime 差分（合成时钟，量化到刷新周期），比墙钟更贴近真实帧节拍；
/// 会话/显示器切换会让时钟跳变，表现为异常大的间隔，统一按 FrameIdleMs 过滤。
/// </summary>
internal sealed class FrameRateMeter
{
    private const int Capacity = 256;
    private const double WindowMs = 1000;
    private const double FrameIdleMs = 500;

    private readonly (double Time, double Delta)[] frames = new (double, double)[Capacity];
    private TimeSpan lastFrameTime;
    private bool hasFrameTime;
    private int cursor;
    private int arrived;
    private double idleMs;

    /// <summary>时间窗内的平均帧率；连续 FrameIdleMs 没有帧时为 0。</summary>
    internal double Rate { get; private set; }

    /// <summary>时间窗内最大的帧间隔（毫秒）。</summary>
    internal double LongestFrame { get; private set; }

    internal void Start( )
    {
        Array.Clear(frames);
        hasFrameTime = false;
        cursor = 0;
        arrived = 0;
        idleMs = 0;
        Rate = 0;
        LongestFrame = 0;

        CompositionTarget.Rendering -= OnFrame;
        CompositionTarget.Rendering += OnFrame;
    }

    internal void Stop( )
    {
        CompositionTarget.Rendering -= OnFrame;
    }

    /// <summary>
    /// 由调用方按固定节拍调用，<paramref name="intervalMs"/> 为该节拍的名义间隔（用于累计无帧时长）。
    /// </summary>
    internal void Sample(double intervalMs)
    {
        idleMs = arrived > 0 ? 0 : idleMs + intervalMs;
        arrived = 0;

        if (idleMs >= FrameIdleMs)
        {
            Rate = 0;
            LongestFrame = 0;
            return;
        }

        var cutoff = lastFrameTime.TotalMilliseconds - WindowMs;
        double sum = 0;
        double longest = 0;
        var count = 0;
        foreach ((var time, var delta) in frames)
        {
            if (delta <= 0 || time < cutoff)
            {
                continue;
            }

            sum += delta;
            longest = Math.Max(longest, delta);
            count++;
        }

        Rate = count == 0 ? 0 : 1000 / (sum / count);
        LongestFrame = longest;
    }

    private void OnFrame(object? o, EventArgs e)
    {
        var time = ((RenderingEventArgs) e).RenderingTime;
        if (hasFrameTime)
        {
            var delta = (time - lastFrameTime).TotalMilliseconds;
            if (delta is > 0 and < FrameIdleMs)
            {
                frames[cursor] = (time.TotalMilliseconds, delta);
                cursor = (cursor + 1) % Capacity;
                arrived++;
            }
        }

        lastFrameTime = time;
        hasFrameTime = true;
    }
}
