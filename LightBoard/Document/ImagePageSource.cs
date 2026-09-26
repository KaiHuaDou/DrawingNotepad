using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightBoard;

/// <summary>
/// 图片渲染源：BMP/GIF/ICO/JPEG/PNG/TIFF 均由 BitmapDecoder 按容器签名自动识别解码，
/// </summary>
internal sealed class ImagePageSource : PageSource
{
    private readonly BitmapFrame frame;

    private ImagePageSource(BitmapFrame frame)
    {
        this.frame = frame;
        PageCount = 1;
    }

    public override int PageCount { get; }

    public static ImagePageSource Open(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        BitmapDecoder decoder;
        try
        {
            decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException)
        {
            throw new InvalidDataException("无法打开图片（格式不受支持或文件损坏）", ex);
        }

        if (decoder.Frames.Count == 0)
        {
            throw new InvalidDataException("无法打开图片（文件中没有图像帧）");
        }

        var frame = decoder.Frames[0];

        return new ImagePageSource(frame);
    }

    public override ImageSource? GetPage(int index)
    {
        return index == 0 ? frame : null;
    }

    public override void Dispose( )
    {
        // 帧数据已随 OnLoad 常驻内存，无外部句柄需释放，交由 GC 回收。
    }
}