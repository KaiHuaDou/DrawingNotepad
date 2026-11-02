#if OPENCV
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

using OpenCvSharp;

namespace LightBoard;

// 展台采集：优先 MSMF（硬解、共享、低缓冲），超时或失败改试 DSHOW。
// 采集线程连续取帧清空后端环形缓冲，单槽只保留最新帧；持锁拷贝期间帧不会被释放。
internal sealed class CameraCapture : IDisposable
{
    // videoio 属性编号：CAP_PROP_HW_ACCELERATION = 50，CAP_PROP_BUFFERSIZE = 38；
    // HW_ACCELERATION 取值 1 = VIDEO_ACCELERATION_ANY（MJPG 交核显解码）
    private const VideoCaptureProperties HwAccelerationProp = VideoCaptureProperties.HwAcceleration;
    private const VideoCaptureProperties BufferSizeProp = VideoCaptureProperties.BufferSize;
    private const double HwAccelerationAny = 1;
    private const int MjpgFourCc = 'M' | ('J' << 8) | ('P' << 16) | ('G' << 24);

    // 探测不到原生档时的固定尺寸
    private const int FallbackWidth = 1920;
    private const int FallbackHeight = 1080;
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(10);

    // 释放前给读取线程的退出时限：超过说明它卡在 Read 里，由线程收尾释放
    private const int ReaderJoinMilliseconds = 500;

    private readonly Lock gate = new( );
    private readonly List<VideoCapture> lateCaptures = [];
    private Mat? slot;
    private Mat? readerBuffer;

    // 冻结那一刻的帧副本；槽里的帧仍随采集前进，拍照取这份拷贝才与画面一致
    private Mat? frozen;
    private VideoCapture? capture;
    private Thread? reader;
    private volatile bool running;
    private bool stopRequested;
    private bool disposed;

    // 每次换帧自增，渲染节拍用它跳过重复帧
    public int FrameId { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public double FrameRate { get; private set; }

    // 设备断开（读取失败或被占用）时触发一次，回调在采集线程
    public event Action? Stopped;

    // 打开设备并按它的原生档协商；两种后端都失败返回 false。必须在后台线程 await。
    public async Task<bool> OpenAsync(CameraDevice device)
    {
        var capture = await AwaitOpen(TryOpenLater(VideoCaptureAPIs.MSMF, device));
        capture ??= await AwaitOpen(TryOpenLater(VideoCaptureAPIs.DSHOW, device));

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

    // 把当前画面写入 BGR24 位图；帧尺寸与位图不符时跳过（位图按打开时协商尺寸创建）
    public bool BlitLatestTo(WriteableBitmap bitmap)
    {
        lock (gate)
        {
            var source = frozen ?? slot;
            if (source?.Empty( ) != false)
            {
                return false;
            }

            if (bitmap.PixelWidth != source.Width || bitmap.PixelHeight != source.Height)
            {
                return false;
            }

            var stride = (int) source.Step( );
            bitmap.WritePixels(new Int32Rect(0, 0, source.Width, source.Height), source.Data, stride * source.Height, stride);
            return true;
        }
    }

    // 拍照路径：把当前画面深拷贝到目标 Mat；无帧返回 false
    public bool CopyLatestTo(Mat destination)
    {
        lock (gate)
        {
            var source = frozen ?? slot;
            if (source?.Empty( ) != false)
            {
                return false;
            }

            source.CopyTo(destination);
            return true;
        }
    }

    // 冻结时留一份当前帧，之后拍照不再取新帧
    public void Freeze( )
    {
        lock (gate)
        {
            frozen?.Dispose( );
            frozen = slot?.Empty( ) == false ? slot.Clone( ) : null;
        }
    }

    public void Unfreeze( )
    {
        lock (gate)
        {
            frozen?.Dispose( );
            frozen = null;
        }
    }

    public void Dispose( )
    {
        Thread? reader;
        lock (gate)
        {
            disposed = true;
            stopRequested = true;
            running = false;
            reader = this.reader;
            this.reader = null;
        }

        // Mat 与 VideoCapture 只能在读取线程退出后释放：它可能正把帧读进 readerBuffer。
        // 卡在 Read 里时不再等待，由读取线程自己的收尾释放，宁可延后也不能释放正在使用的实例
        if (reader is not null && reader != Thread.CurrentThread)
        {
            reader.Join(ReaderJoinMilliseconds);
        }

        if (reader?.IsAlive != true)
        {
            ReleaseResources( );
        }

        List<VideoCapture> late;
        lock (gate)
        {
            late = [.. lateCaptures];
            lateCaptures.Clear( );
        }

        foreach (var item in late)
        {
            item.Dispose( );
        }
    }

    private void ReleaseResources( )
    {
        VideoCapture? capture;
        lock (gate)
        {
            slot?.Dispose( );
            slot = null;
            readerBuffer?.Dispose( );
            readerBuffer = null;
            frozen?.Dispose( );
            frozen = null;
            capture = this.capture;
            this.capture = null;
        }

        capture?.Dispose( );
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

    private static VideoCapture? TryOpen(VideoCaptureAPIs api, CameraDevice device)
    {
        VideoCapture capture;
        try
        {

#pragma warning disable CA2000

            capture = new VideoCapture(device.Index, api);

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

        Configure(capture, device.Format);
        return capture;
    }

    private static Task<VideoCapture?> TryOpenLater(VideoCaptureAPIs api, CameraDevice device)
    {
        return Task.Run(( ) => TryOpen(api, device));
    }

    // set 不保证生效，实际尺寸与帧率以 OpenAsync 里的 Get 回读为准
    private static void Configure(VideoCapture capture, CameraFormat format)
    {
        capture.Set(HwAccelerationProp, HwAccelerationAny);

        // MJPG 的可用档位受像素格式限制：像素格式先定，再定尺寸
        if (format.IsEmpty || format.Mjpg)
        {
            capture.Set(VideoCaptureProperties.FourCC, MjpgFourCc);
        }

        if (format.IsEmpty)
        {
            capture.Set(VideoCaptureProperties.FrameWidth, FallbackWidth);
            capture.Set(VideoCaptureProperties.FrameHeight, FallbackHeight);
        }
        else
        {
            capture.Set(VideoCaptureProperties.FrameWidth, format.Width);
            capture.Set(VideoCaptureProperties.FrameHeight, format.Height);
            if (format.Fps > 0)
            {
                capture.Set(VideoCaptureProperties.Fps, format.Fps);
            }
        }

        capture.Set(BufferSizeProp, 1);
    }

    private void ReadLoop( )
    {
        try
        {
            while (running)
            {
                Mat buffer;
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    buffer = readerBuffer ??= new Mat( );
                }

                bool read;
                try
                {
                    read = capture?.Read(buffer) ?? false;
                }
                catch (Exception e)
                {
                    App.LogException(e);
                    break;
                }

                if (!read || buffer.Empty( ))
                {
                    break;
                }

                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    // 双缓冲换槽：刚读完的帧进槽，原槽变为下一帧的读取目标，全程无帧拷贝
                    var old = slot;
                    slot = buffer;
                    readerBuffer = old;
                    FrameId++;
                }
            }
        }
        finally
        {
            // 帧缓冲与设备只能由读取线程自己释放：它退出前可能正把帧读进读者缓冲
            ReleaseResources( );
            if (!stopRequested)
            {
                Stopped?.Invoke( );
            }
        }
    }
}
#endif
