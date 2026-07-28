using System;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Threading;

using Ookii.Dialogs.Wpf;

using SingleInstanceCore;

namespace LightBoard;

public partial class App : Application, ISingleInstance
{
    public static readonly string AppPath = Path.GetDirectoryName(Environment.ProcessPath)!;
    public static string? PendingOpen { get; set; }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args?.Length > 0)
            {
                PendingOpen = args[0];
            }

            App app = new( );
            app.InitializeComponent( );
            app.Run( );
            SingleInstance.Cleanup( );
        }
    }

    private void AppStartup(object o, StartupEventArgs e)
    {
        if (!this.InitializeAsFirstInstance("LightBoardInstanceInvariantVersion"))
        {
            Current.Shutdown( );
        }

        Directory.CreateDirectory(Path.Join(AppPath, "recover"));
    }

    public void OnInstanceInvoked(string[] args)
    {
        Current.MainWindow.Show( );
        Current.MainWindow.Activate( );
    }

    private void AppDispatcherUnhandledException(object o, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);

        (Current.MainWindow as MainWindow)!.SaveStrokes(Path.Join(AppPath, $"{DateTime.Now.Ticks}.isf"));

        ShowException(e.Exception, "程序即将关闭。错误日志已记录。墨迹已备份。");

        Application.Current.Shutdown(1);
    }

    public static void ShowInfo(string message)
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = message,
            MainIcon = TaskDialogIcon.Information,
            Content = message,
        };
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }

    public static void ShowException(Exception e, string message)
    {
        var details = $"{e.Message}\n{e.StackTrace}";
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "程序出现错误",
            MainIcon = TaskDialogIcon.Error,
            Content = message,
            ExpandedInformation = details,
        };
        var copyButton = new TaskDialogButton("复制错误信息");
        dialog.Buttons.Add(copyButton);
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        var result = dialog.ShowDialog( );
        if (result == copyButton)
        {
            Clipboard.SetText(details);
        }
    }

    public static void LogException(Exception e)
    {
        File.AppendAllText(Path.Join(AppPath, "error.log"), $"\n{e.Message}\n{e.StackTrace}\n");
    }
}
