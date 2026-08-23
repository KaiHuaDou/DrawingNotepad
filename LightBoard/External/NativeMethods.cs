using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LightBoard.External;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

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
            SwitchToThisWindow(hWnd, true);
        }
    }
}
