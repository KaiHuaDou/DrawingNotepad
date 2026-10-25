using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace LightBoard.External;

internal static class ProgramIcon
{
    /// <summary>
    /// 提取有可见窗口的进程的程序图标；未运行、没有可见窗口或提取失败返回 null。
    /// </summary>
    public static ImageSource? FromProcess(string processName, int size)
    {
        var path = ProcessPath(processName);

        return path is null ? null : FromFile(path, size);
    }

    /// <summary>
    /// 用 Shell 渲染提取文件图标（统一处理 exe/lnk/ico，按请求尺寸返回位图）。
    /// </summary>
    public static ImageSource? FromFile(string path, int size)
    {
        PInvoke.SHCreateItemFromParsingName<IShellItemImageFactory>(path, null, out var factory);

        if (factory is null)
        {
            return null;
        }

        try
        {
            factory.GetImage(new SIZE(size, size), SIIGBF.SIIGBF_BIGGERSIZEOK, out var bitmap);

            using (bitmap)
            {
                if (bitmap.IsInvalid)
                {
                    return null;
                }

                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap.DangerousGetHandle( ),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions( ));

                if (source.CanFreeze)
                {
                    source.Freeze( );
                }

                return source;
            }
        }
        catch (Exception)
        {
            // 图标是装饰性内容，提取异常不向上抛，调用方保持字形回退
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>
    /// 取有可见窗口的进程的主模块路径；没有可见窗口或路径不可读时返回 null。
    /// </summary>
    private static string? ProcessPath(string processName)
    {
        using var process = NativeMethods.VisibleProcess(processName);

        if (process is null)
        {
            return null;
        }

        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            // 目标进程可能拒绝访问主模块或已退出
            return null;
        }
    }
}
