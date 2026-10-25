using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LightBoard.External;

internal static class NativeMethods
{
    public static void ClearWindowRegion(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        _ = PInvoke.SetWindowRgn((HWND) hWnd, default, true);
    }

    public static void SetWindowRegion(IntPtr hWnd, double windowWidth, double windowHeight, DpiScale dpi, Rect borderRect)
    {
        var winW = (int) Math.Ceiling(windowWidth * dpi.DpiScaleX);
        var winH = (int) Math.Ceiling(windowHeight * dpi.DpiScaleY);
        var holeL = (int) Math.Round(borderRect.Left * dpi.DpiScaleX);
        var holeT = (int) Math.Round(borderRect.Top * dpi.DpiScaleY);
        var holeR = (int) Math.Round(borderRect.Right * dpi.DpiScaleX);
        var holeB = (int) Math.Round(borderRect.Bottom * dpi.DpiScaleY);

        var hRgnWindow = PInvoke.CreateRectRgn(0, 0, winW, winH);
        var hRgnHole = PInvoke.CreateRectRgn(holeL, holeT, holeR, holeB);
        _ = PInvoke.CombineRgn(hRgnWindow, hRgnWindow, hRgnHole, RGN_COMBINE_MODE.RGN_DIFF);

        _ = PInvoke.SetWindowRgn((HWND) hWnd, hRgnWindow, true);

        PInvoke.DeleteObject(hRgnHole);
    }

    public static void SwitchTo(string processName)
    {
        using var process = VisibleProcess(processName);

        var hWnd = process?.MainWindowHandle ?? IntPtr.Zero;

        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        var window = (HWND) hWnd;

        RestoreWindow(window);
        PInvoke.SetForegroundWindow(window);
    }

    /// <summary>
    /// 同名进程中第一个有可见主窗口的进程；没有时返回 null。调用方负责释放。
    /// </summary>
    public static Process? VisibleProcess(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            if (process.MainWindowHandle == IntPtr.Zero)
            {
                process.Dispose( );
                continue;
            }

            return process;
        }

        return null;
    }

    /// <summary>
    /// 仅不可见的窗口需要还原：最小化且最小化前是最大化时按最大化还原
    /// </summary>
    private static void RestoreWindow(HWND window)
    {
        var placement = new WINDOWPLACEMENT { length = (uint) Marshal.SizeOf<WINDOWPLACEMENT>() };

        if (!PInvoke.GetWindowPlacement(window, ref placement))
        {
            return;
        }

        if (placement.showCmd == SHOW_WINDOW_CMD.SW_SHOWMINIMIZED)
        {
            var maximized = (placement.flags & WINDOWPLACEMENT_FLAGS.WPF_RESTORETOMAXIMIZED) != 0;
            PInvoke.ShowWindow(window, maximized ? SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED : SHOW_WINDOW_CMD.SW_RESTORE);
        }
        else if (placement.showCmd == SHOW_WINDOW_CMD.SW_HIDE)
        {
            PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_SHOWNA);
        }
    }
}
