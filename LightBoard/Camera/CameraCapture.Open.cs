#if OPENCV
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using OpenCvSharp;

namespace LightBoard;

// 打开设备与画面档协商：优先 MSMF，超时或失败改试 DSHOW
internal sealed partial class CameraCapture
{
    // MJPG 的 fourcc 值，DSHOW 用它选采集像素格式
    private const int MjpgFourCc = 'M' | ('J' << 8) | ('P' << 16) | ('G' << 24);

    // 探测不到原生档时的固定尺寸
    private const int FallbackWidth = 1920;
    private const int FallbackHeight = 1080;

    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(10);

    // 打开动作可能阻塞远超时限；超时放弃等待后，完成的实例转入这里由 Dispose 回收
    private readonly List<VideoCapture> lateCaptures = [];

    // 打开设备并按它的原生档协商；两种后端都失败返回 false。必须在后台线程 await。
    public async Task<bool> OpenAsync(int index, CameraFormat format)
    {
        var capture = await AwaitOpen(Task.Run(( ) => TryOpen(VideoCaptureAPIs.MSMF, index, format)))
            ?? await AwaitOpen(Task.Run(( ) => TryOpen(VideoCaptureAPIs.DSHOW, index, format)));

        if (capture is null)
        {
            return false;
        }

        FrameWidth = (int) capture.Get(VideoCaptureProperties.FrameWidth);
        FrameHeight = (int) capture.Get(VideoCaptureProperties.FrameHeight);
        FrameRate = capture.Get(VideoCaptureProperties.Fps);
        if (FrameWidth <= 0 || FrameHeight <= 0)
        {
            capture.Dispose( );
            return false;
        }

        this.capture = capture;
        running = true;
        reader = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "CameraReader"
        };
        reader.Start( );
        return true;
    }

    private static VideoCapture? TryOpen(VideoCaptureAPIs api, int index, CameraFormat format)
    {
        VideoCapture capture;
        try
        {

#pragma warning disable CA2000

            capture = new VideoCapture(index, api);

#pragma warning restore CA2000

            // 原生构造对打开失败不抛错，返回未打开的实例，失败路径自行释放
            if (!capture.IsOpened( ))
            {
                capture.Dispose( );
                return null;
            }
        }
        catch (Exception e)
        {
            App.LogException(e);
            return null;
        }

        Configure(capture, format, api);
        return capture;
    }

    // set 不保证生效，实际尺寸与帧率以 OpenAsync 里的 Get 回读为准
    private static void Configure(VideoCapture capture, CameraFormat format, VideoCaptureAPIs api)
    {
        // DSHOW 用 FOURCC 选采集像素格式；MSMF 的该属性只影响输出格式，设了会被拒绝
        if (api == VideoCaptureAPIs.DSHOW && (format.IsEmpty || format.Mjpg))
        {
            Apply(capture, VideoCaptureProperties.FourCC, MjpgFourCc);
        }

        // 宽、高两项都写，按差异跳过时画面全绿
        Apply(capture, VideoCaptureProperties.FrameWidth, format.IsEmpty ? FallbackWidth : format.Width);
        Apply(capture, VideoCaptureProperties.FrameHeight, format.IsEmpty ? FallbackHeight : format.Height);

        if (format.Fps > 0)
        {
            Apply(capture, VideoCaptureProperties.Fps, format.Fps);
        }
    }

    private static void Apply(VideoCapture capture, VideoCaptureProperties property, double value)
    {
        if (!capture.Set(property, value))
        {
            App.LogException(new NotSupportedException($"摄像头属性 {property} 无法设置"));
        }
    }

    // 打开动作可能阻塞远超时限；超时放弃等待后，完成的实例转入 lateCaptures 由 Dispose 回收
    private async Task<VideoCapture?> AwaitOpen(Task<VideoCapture?> openTask)
    {
        var done = await Task.WhenAny(openTask, Task.Delay(OpenTimeout));
        if (done == openTask)
        {
            return await openTask;
        }

        _ = openTask.ContinueWith(t =>
        {
            if (t.Result is not null)
            {
                DiscardLate(t.Result);
            }
        }, TaskScheduler.Default);
        return null;
    }

    private void DiscardLate(VideoCapture late)
    {
        lock (gate)
        {
            if (disposed)
            {
                late.Dispose( );
                return;
            }

            lateCaptures.Add(late);
        }
    }
}
#endif
