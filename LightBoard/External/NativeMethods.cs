using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace LightBoard.External;

internal static partial class NativeMethods
{
    private const int RGN_DIFF = 4;

    private const int SW_RESTORE = 9;

    public static void ClearWindowRegion(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowRgn(hWnd, IntPtr.Zero, true);
    }

    public static void SetWindowRegion(IntPtr hWnd, double windowWidth, double windowHeight, DpiScale dpi, Rect borderRect)
    {
        var winW = (int) Math.Ceiling(windowWidth * dpi.DpiScaleX);
        var winH = (int) Math.Ceiling(windowHeight * dpi.DpiScaleY);
        var holeL = (int) Math.Round(borderRect.Left * dpi.DpiScaleX);
        var holeT = (int) Math.Round(borderRect.Top * dpi.DpiScaleY);
        var holeR = (int) Math.Round(borderRect.Right * dpi.DpiScaleX);
        var holeB = (int) Math.Round(borderRect.Bottom * dpi.DpiScaleY);

        var hRgnWindow = CreateRectRgn(0, 0, winW, winH);
        var hRgnHole = CreateRectRgn(holeL, holeT, holeR, holeB);
        _ = CombineRgn(hRgnWindow, hRgnWindow, hRgnHole, RGN_DIFF);

        SetWindowRgn(hWnd, hRgnWindow, true);

        DeleteObject(hRgnHole);
    }

    public static void SwitchTo(string processName)
    {
        var processes = Process.GetProcessesByName(processName);

        if (processes.Length == 0)
        {
            return;
        }

        var hWnd = processes[0].MainWindowHandle;

        if (hWnd != IntPtr.Zero)
        {
            ShowWindow(hWnd, SW_RESTORE);
            SetForegroundWindow(hWnd);
        }
    }

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int CombineRgn(IntPtr hrgnDst, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int iMode);

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr CreateRectRgn(int nLeft, int nTop, int nRight, int nBottom);

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
