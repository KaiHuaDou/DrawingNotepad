#if OPENCV
using System;

using OpenCvSharp;

namespace LightBoard;

// 展台画面的取向：先绕画面中心镜像，再按 90 度步进旋转。目标 Mat 跨帧复用，避免每帧分配
internal sealed class CameraOrientation : IDisposable
{
    private Mat? mirrored;
    private Mat? rotated;

    /// <summary>
    /// 按 <paramref name="angle"/> 与 <paramref name="mirror"/> 变换画面；返回的 Mat 由本实例持有，下次调用前有效
    /// </summary>
    public Mat Apply(Mat source, int angle, bool mirror)
    {
        var frame = mirror ? Flip(source) : source;
        return angle == 0 ? frame : Rotate(frame, angle);
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
        mirrored?.Dispose( );
        rotated?.Dispose( );
        mirrored = null;
        rotated = null;
    }

    private Mat Flip(Mat source)
    {
        mirrored ??= new Mat( );
        Cv2.Flip(source, mirrored, FlipMode.Y);
        return mirrored;
    }

    private Mat Rotate(Mat source, int angle)
    {
        rotated ??= new Mat( );
        Cv2.Rotate(source, rotated, angle switch
        {
            90 => RotateFlags.Rotate90Clockwise,
            180 => RotateFlags.Rotate180,
            _ => RotateFlags.Rotate90Counterclockwise
        });
        return rotated;
    }
}
#endif
