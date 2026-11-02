using System;
using System.Windows;

namespace LightBoard;

// 展台画面的取向几何：镜像与旋转都绕画面中心，预览与照片共用同一套尺寸推导
internal static class CameraView
{
    /// <summary>
    /// 画面旋转 <paramref name="angle"/> 度后在 <paramref name="area"/> 内等比放大并居中的矩形；
    /// 任一尺寸非正时返回空矩形
    /// </summary>
    public static Rect Fit(Size frame, int angle, Size area)
    {
        if (!(frame.Width > 0) || !(frame.Height > 0) || !(area.Width > 0) || !(area.Height > 0))
        {
            return Rect.Empty;
        }

        var rotated = Rotated(frame, angle);
        var scale = Math.Min(area.Width / rotated.Width, area.Height / rotated.Height);
        var width = rotated.Width * scale;
        var height = rotated.Height * scale;
        return new Rect((area.Width - width) / 2, (area.Height - height) / 2, width, height);
    }

    /// <summary>
    /// 旋转前的布局尺寸：90 与 270 度时宽高互换
    /// </summary>
    public static Size BeforeRotation(Size rotated, int angle)
    {
        return angle % 180 == 0 ? rotated : new Size(rotated.Height, rotated.Width);
    }

    private static Size Rotated(Size frame, int angle)
    {
        return angle % 180 == 0 ? frame : new Size(frame.Height, frame.Width);
    }
}
