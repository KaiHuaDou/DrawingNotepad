#if OPENCV
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media.Imaging;

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace LightBoard;

// 展台采集：采集线程连续取帧清空后端环形缓冲，两级换槽，取向在采集线程完成；
// 消费侧读槽帧全程持锁，持锁期间帧不会被换下
internal sealed partial class CameraCapture : IDisposable
{
    // 释放前给读取线程的退出时限：超过说明它卡在 Read 里，由线程收尾释放
    private const int ReaderJoinMilliseconds = 500;

    private readonly Lock gate = new( );

    // 采集侧取向实例在锁外逐帧使用；消费侧实例只服务冻结帧与拍照，两侧缓冲互不相交
    private readonly CameraOrientation readerOrientation = new( );
    private readonly CameraOrientation consumerOrientation = new( );

    // readBuffer 与 rawSlot 成对换位，orientBuffer 与 frameSlot 成对换位；
    // rawSlot 持有最近一帧的原始画面，frameSlot 持有取向后的画面
    private Mat? readBuffer;
    private Mat? rawSlot;
    private Mat? orientBuffer;
    private Mat? frameSlot;

    // 冻结路径取向输出的落点，锁内懒分配
    private Mat? blitBuffer;

    // 冻结那一刻的原始帧副本；槽里的帧仍随采集前进，冻结预览与拍照对这份拷贝取向
    private Mat? frozen;
    private VideoCapture? capture;
    private Thread? reader;
    private volatile bool running;
    private bool stopRequested;
    private bool disposed;

    // 预览取向，锁内读写；采集线程每帧开始时取入局部变量，一帧内固定
    private int angle;
    private bool mirror;

    // 每次换帧自增，渲染节拍用它跳过重复帧
    public int FrameId { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public double FrameRate { get; private set; }

    // 设备断开（读取失败或被占用）时触发一次，回调在采集线程
    public event Action? Stopped;

    // 预览取向；采集线程从下一帧起生效
    public void SetOrientation(int newAngle, bool newMirror)
    {
        lock (gate)
        {
            angle = newAngle;
            mirror = newMirror;
        }
    }

    // 把当前画面写入 BGR24 位图；未冻结直接用取向后的槽帧，冻结帧按参数取向，尺寸不符时跳过
    public bool BlitLatestTo(WriteableBitmap bitmap, int angle, bool mirror)
    {
        lock (gate)
        {
            var frame = frozen is not null ? OrientFrozen(angle, mirror) : frameSlot;
            if (frame?.Empty( ) != false
                || bitmap.PixelWidth != frame.Width || bitmap.PixelHeight != frame.Height)
            {
                return false;
            }

            // 转换直写后备缓冲，要求位图尺寸与 Mat 通道一致（BGR24 对应 8UC3）
            WriteableBitmapConverter.ToWriteableBitmap(frame, bitmap);
            return true;
        }
    }

    // 拍照路径：把当前画面按取向写入目标 Mat；无帧返回 false
    // 槽帧已带取向，只有冻结的原始帧需要取向
    public bool CopyLatestTo(Mat destination, int angle, bool mirror)
    {
        lock (gate)
        {
            var frozen = this.frozen;
            if (frozen is not null)
            {
                consumerOrientation.Apply(frozen, destination, angle, mirror);
                return true;
            }

            var frame = frameSlot;
            if (frame?.Empty( ) != false)
            {
                return false;
            }

            frame.CopyTo(destination);
            return true;
        }
    }

    // 冻结时留一份原始帧，之后冻结预览与拍照都取向这份拷贝
    public void Freeze( )
    {
        lock (gate)
        {
            frozen?.Dispose( );
            frozen = rawSlot?.Empty( ) == false ? rawSlot.Clone( ) : null;
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

        // Mat 与 VideoCapture 只能在读取线程退出后释放：它可能正把帧读进读者缓冲。
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

    // 冻结帧按取向写入消费侧缓冲；输出跨帧复用，调用方持锁
    private Mat OrientFrozen(int angle, bool mirror)
    {
        var frame = blitBuffer ??= new Mat( );
        consumerOrientation.Apply(frozen!, frame, angle, mirror);
        return frame;
    }

    private void ReleaseResources( )
    {
        VideoCapture? capture;
        lock (gate)
        {
            readerOrientation.Dispose( );
            consumerOrientation.Dispose( );
            readBuffer?.Dispose( );
            rawSlot?.Dispose( );
            orientBuffer?.Dispose( );
            frameSlot?.Dispose( );
            blitBuffer?.Dispose( );
            frozen?.Dispose( );
            readBuffer = null;
            rawSlot = null;
            orientBuffer = null;
            frameSlot = null;
            blitBuffer = null;
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

                    buffer = readBuffer ??= new Mat( );
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

                Mat oriented;
                int frameAngle;
                bool frameMirror;
                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    // 一级提交：原始帧进 rawSlot，读取目标换为原 rawSlot
                    (rawSlot, readBuffer) = (buffer, rawSlot);
                    oriented = orientBuffer ??= new Mat( );
                    frameAngle = angle;
                    frameMirror = mirror;
                }

                readerOrientation.Apply(buffer, oriented, frameAngle, frameMirror);

                lock (gate)
                {
                    if (disposed)
                    {
                        return;
                    }

                    // 二级提交：取向后的画面才对消费侧可见
                    (frameSlot, orientBuffer) = (oriented, frameSlot);
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
