using System;
using System.ComponentModel;
using System.Diagnostics;
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
    /// 提取运行中进程的程序图标；进程未运行或提取失败返回 null。
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
    /// 取运行中进程的主模块路径；同名的多个进程逐个尝试，全部不可达时返回 null。
    /// </summary>
    private static string? ProcessPath(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            string? path;

            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                // 目标进程可能拒绝访问主模块或已退出枚举窗口
                path = null;
            }

            process.Dispose( );

            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }
}
