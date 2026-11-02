#if OPENCV
using System;
using System.IO;

using OpenCvSharp;

namespace LightBoard;

// 展台拍照：最新帧按预览的取向物化为 PNG，写入程序目录的 photos
internal static class CameraPhoto
{
    // 线程池线程执行；capture 为 null 或无帧返回 null
    public static string? Take(CameraCapture? capture, int angle, bool mirror)
    {
        using var frame = new Mat( );
        using var orientation = new CameraOrientation( );
        if (capture?.CopyLatestTo(frame) != true)
        {
            Log("无可用帧");
            return null;
        }

        var photo = orientation.Apply(frame, angle, mirror);
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
        App.LogException(new InvalidOperationException(message));
    }
}
#endif
