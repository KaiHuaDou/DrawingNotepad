#if OPENCV
using System;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Media.MediaFoundation;

namespace LightBoard;

/// <summary>
/// 采集设备的原生画面档；Fps 为 0 表示设备未声明帧率
/// </summary>
internal readonly record struct CameraFormat(int Width, int Height, double Fps, bool Mjpg)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

// 设备的原生媒体类型：分辨率优先，同一尺寸优先 MJPG，再取最高帧率
internal static class CameraFormats
{
    /// <summary>
    /// 设备可申请的最大画面档；探测失败（设备被占用、没有视频流）时返回空值
    /// </summary>
    public static unsafe CameraFormat Best(IMFActivate_unmanaged* activate)
    {
        try
        {
            var pointer = activate->ActivateObject(typeof(IMFMediaSource).GUID);
            if (pointer is null)
            {
                return default;
            }

            // GetObjectForIUnknown 自带一次引用，原始指针另需释放
            var source = (IMFMediaSource) Marshal.GetObjectForIUnknown((nint) pointer);
            Marshal.Release((nint) pointer);

            try
            {
                if (PInvoke.MFCreateSourceReaderFromMediaSource(source, null, out var reader).Failed)
                {
                    return default;
                }

                try
                {
                    return Best(reader);
                }
                finally
                {
                    Marshal.ReleaseComObject(reader);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(source);
            }
        }
        catch (Exception e)
        {
            App.LogException(e);
            return default;
        }
    }

    private static CameraFormat Best(IMFSourceReader reader)
    {
        var best = default(CameraFormat);
        for (uint index = 0; ; index++)
        {
            if (!TryGetType(reader, index, out var type))
            {
                return best;
            }

            try
            {
                var format = Read(type);
                if (IsBetter(format, best))
                {
                    best = format;
                }
            }
            catch (COMException)
            {
                // 类型缺少尺寸或帧率属性：跳过
            }
            finally
            {
                Marshal.ReleaseComObject(type);
            }
        }
    }

    // 枚举到头时 GetNativeMediaType 以 MF_E_NO_MORE_TYPES 抛错
    private static bool TryGetType(IMFSourceReader reader, uint index, out IMFMediaType type)
    {
        try
        {
            reader.GetNativeMediaType(unchecked((uint) MF_SOURCE_READER_CONSTANTS.MF_SOURCE_READER_FIRST_VIDEO_STREAM), index, out type);
            return true;
        }
        catch (COMException)
        {
            type = null!;
            return false;
        }
    }

    // MF_MT_FRAME_SIZE 与 MF_MT_FRAME_RATE 都是高 32 位分子、低 32 位分母的打包值
    private static unsafe CameraFormat Read(IMFMediaType type)
    {
        var sizeKey = PInvoke.MF_MT_FRAME_SIZE;
        type.GetUINT64(&sizeKey, out var size);

        var rateKey = PInvoke.MF_MT_FRAME_RATE;
        type.GetUINT64(&rateKey, out var rate);

        var subtypeKey = PInvoke.MF_MT_SUBTYPE;
        Guid subtype;
        type.GetGUID(&subtypeKey, &subtype);

        var denominator = (uint) (rate & 0xFFFF_FFFF);
        return new CameraFormat(
            (int) (size >> 32),
            (int) (size & 0xFFFF_FFFF),
            denominator == 0 ? 0 : (double) (uint) (rate >> 32) / denominator,
            subtype == PInvoke.MFVideoFormat_MJPG);
    }

    private static bool IsBetter(CameraFormat candidate, CameraFormat current)
    {
        if (current.IsEmpty)
        {
            return true;
        }

        var candidatePixels = (long) candidate.Width * candidate.Height;
        var currentPixels = (long) current.Width * current.Height;
        if (candidatePixels != currentPixels)
        {
            return candidatePixels > currentPixels;
        }

        // 同一尺寸下 MJPG 的码率更低，往往还有更高的帧率档
        if (candidate.Mjpg != current.Mjpg)
        {
            return candidate.Mjpg;
        }

        return candidate.Fps > current.Fps;
    }
}
#endif
