using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LightBoard.External;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll", EntryPoint = "SwitchToThisWindowA")]
    public static partial void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);

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
