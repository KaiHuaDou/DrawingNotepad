using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LightBoard.External;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial void SetForegroundWindow(IntPtr hWnd);

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
            SetForegroundWindow(hWnd);
        }
    }
}
