#if OPENCV
using System;

using OpenCvSharp;

namespace LightBoard;

// 展台画面的取向：先绕画面中心镜像，再按 90 度步进旋转；镜像加旋转的组合经内部 mid 缓冲
internal sealed class CameraOrientation : IDisposable
{
    private Mat? mid;

    // 按 angle 与 mirror 变换画面写入 output；output 与 source 须为不同实例，跨帧复用由调用方负责
    public void Apply(Mat source, Mat output, int angle, bool mirror)
    {
        if (angle == 0)
        {
            if (mirror)
            {
                Flip(source, output);
            }
            else
            {
                source.CopyTo(output);
            }

            return;
        }

        if (mirror)
        {
            var buffer = Mid( );
            Flip(source, buffer);
            Rotate(buffer, output, angle);
            return;
        }

        Rotate(source, output, angle);
    }

    /// <summary>
    /// 画面旋转后的像素尺寸
    /// </summary>
    public static (int Width, int Height) PixelSize(int width, int height, int angle)
    {
        return angle % 180 == 0 ? (width, height) : (height, width);
    }

    public void Dispose( )
    {
        mid?.Dispose( );
        mid = null;
    }

    private Mat Mid( )
    {
        mid ??= new Mat( );
        return mid;
    }

    private static void Flip(Mat source, Mat output)
    {
        Cv2.Flip(source, output, FlipMode.Y);
    }

    private static void Rotate(Mat source, Mat output, int angle)
    {
        Cv2.Rotate(source, output, angle switch
        {
            90 => RotateFlags.Rotate90Clockwise,
            180 => RotateFlags.Rotate180,
            _ => RotateFlags.Rotate90Counterclockwise
        });
    }
}
#endif
