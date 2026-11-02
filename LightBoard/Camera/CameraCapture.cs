#if OPENCV
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

using OpenCvSharp;

namespace LightBoard;

// 展台采集：采集线程连续取帧清空后端环形缓冲，单槽只保留最新帧；持锁拷贝期间帧不会被释放
internal sealed partial class CameraCapture : IDisposable
{
    // 释放前给读取线程的退出时限：超过说明它卡在 Read 里，由线程收尾释放
    private const int ReaderJoinMilliseconds = 500;

    private readonly Lock gate = new( );
    private readonly CameraOrientation orientation = new( );
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

    // 把当前画面按预览取向写入 BGR24 位图；位图尺寸与旋转后的画面不符时跳过
    public bool BlitLatestTo(WriteableBitmap bitmap, int angle, bool mirror)
    {
        lock (gate)
        {
            var source = frozen ?? slot;
            if (source?.Empty( ) != false)
            {
                return false;
            }

            var frame = orientation.Apply(source, angle, mirror);
            if (bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
            {
                return false;
            }

            var stride = (int) frame.Step( );
            bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Data, stride * frame.Height, stride);
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
            orientation.Dispose( );
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
