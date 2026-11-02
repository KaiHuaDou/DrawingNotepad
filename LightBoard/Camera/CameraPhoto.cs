#if OPENCV
using System;
using System.IO;
using System.Windows.Media.Imaging;

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace LightBoard;

// 展台拍照：最新帧按预览的取向物化为 PNG 写入 photos，同时带回冻结位图供落页直接显示
internal static class CameraPhoto
{
    // 线程池线程执行；capture 为 null 或无帧返回 null
    public static (string PhotoPath, BitmapSource Photo)? Take(CameraCapture? capture, int angle, bool mirror)
    {
        using var frame = new Mat( );
        if (capture?.CopyLatestTo(frame, angle, mirror) != true)
        {
            Log("无可用帧");
            return null;
        }

        if (!Cv2.ImEncode(".png", frame, out var bytes))
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

            // 冻结后跨线程交给 UI 线程；与 PNG 同一帧，画布显示与下次打开解码结果一致
            var bitmap = frame.ToBitmapSource( );
            bitmap.Freeze( );
            return (path, bitmap);
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
