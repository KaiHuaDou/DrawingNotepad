#if OPENCV
using System;
using System.IO;

using OpenCvSharp;

namespace LightBoard;

// 展台拍照：最新帧按预览的取向物化为 PNG，写入程序目录的 photos
internal static class CameraPhoto
{
    // 线程池线程执行；capture 为 null 或无帧返回 null。
    // 次序与预览一致：先绕画面中心镜像，再绕中心旋转
    public static string? Take(CameraCapture? capture, int angle, bool mirror)
    {
        using var frame = new Mat( );
        if (capture?.CopyLatestTo(frame) != true)
        {
            Log("无可用帧");
            return null;
        }

        using var mirrored = MirrorFrame(frame, mirror);
        using var photo = RotateFrame(mirrored, angle);
        if (photo.Empty( ))
        {
            Log("旋转或镜像后帧为空");
            return null;
        }

        if (!Cv2.ImEncode(".png", photo, out var bytes))
        {
            Log("PNG 编码失败");
            return null;
        }

        try
        {
            var directory = Path.Join(App.AppPath, "photos");
            Directory.CreateDirectory(directory);
            var path = Path.Join(directory, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
            File.WriteAllBytes(path, bytes);
            return path;
        }
        catch (Exception e)
        {
            Log($"写入文件失败：{e.Message}");
            return null;
        }
    }

    // 拍照失败原因进 error.log，便于现场排查
    private static void Log(string message)
    {
        App.LogException(new Exception(message));
    }

    // 顺时针 90° 步进旋转，与预览的 RotateTransform 角度一致
    private static Mat RotateFrame(Mat frame, int angle)
    {
        var dst = new Mat( );
        switch (angle)
        {
            case 90:
                Cv2.Rotate(frame, dst, RotateFlags.Rotate90Clockwise);
                break;
            case 180:
                Cv2.Rotate(frame, dst, RotateFlags.Rotate180);
                break;
            case 270:
                Cv2.Rotate(frame, dst, RotateFlags.Rotate90Counterclockwise);
                break;
            default:
                frame.CopyTo(dst);
                break;
        }

        return dst;
    }

    // 统一返回新实例，调用方一并 using。
    // FlipMode.Y 绕 y 轴翻即左右镜像，与预览的 ScaleX=-1 同向
    private static Mat MirrorFrame(Mat frame, bool mirror)
    {
        var dst = new Mat( );
        if (mirror)
        {
            Cv2.Flip(frame, dst, FlipMode.Y);
        }
        else
        {
            frame.CopyTo(dst);
        }

        return dst;
    }
}
#endif
