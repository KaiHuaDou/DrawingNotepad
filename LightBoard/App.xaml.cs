using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

using Ookii.Dialogs.Wpf;

using SingleInstanceCore;

namespace LightBoard;

public partial class App : Application, ISingleInstance
{
    public static readonly string AppPath = Path.GetDirectoryName(Environment.ProcessPath)!;

    public static readonly Size CanvasSize = new(
        4 * SystemParameters.PrimaryScreenWidth,
        16 * SystemParameters.PrimaryScreenHeight);

    public static readonly Point InitialOffset = new(
        SystemParameters.PrimaryScreenWidth,
        SystemParameters.PrimaryScreenHeight);

    private readonly DispatcherTimer recoverTimer = new( );

    public static string? PendingOpen { get; set; }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                PendingOpen = args[0];
            }

            App app = new( );
            app.InitializeComponent( );
            app.Run( );
            SingleInstance.Cleanup( );
        }
    }

    public static void LogException(Exception e)
    {
        try
        {
            File.AppendAllText(Path.Join(AppPath, "error.log"), $"\nTime:{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{e.Message}\n{e.StackTrace}\n");
        }
        catch { }
    }

    public static void ShowDetailedInfo(string message, string content, string details)
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = message,
            MainIcon = TaskDialogIcon.Warning,
            Content = content,
            ExpandedInformation = details,
        };
        var copyButton = new TaskDialogButton("复制信息");
        dialog.Buttons.Add(copyButton);
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        var result = dialog.ShowDialog( );
        if (result == copyButton)
        {
            SetClipboard(details);
        }
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
            SetClipboard(details);
        }
    }

    public static void ShowInfo(string message, string? content = null)
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = message,
            MainIcon = TaskDialogIcon.Information,
            Content = content ?? message,
        };
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Ok));
        dialog.ShowDialog( );
    }

    // SingleInstanceCore 发布的是 Environment.GetCommandLineArgs() 全量数组：args[0] 是程序自身路径，
    // args[1] 才是第一个真参数；与 Main(string[]) 的 args（不含程序路径）索引语义相反，勿"统一"。
    public void OnInstanceInvoked(string[] args)
    {
        Current.Dispatcher.Invoke(( ) =>
        {
            Current.MainWindow.Show( );
            Current.MainWindow.Activate( );
            if (args?.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                (Current.MainWindow as MainWindow)!.RequestOpenFile(args[1]);
            }
        });
    }

    private static void SetClipboard(string details)
    {
        Current.Dispatcher.Invoke(( ) =>
        {
            try
            {
                Clipboard.Clear( );
                Clipboard.SetDataObject(details, true);
            }
            catch { }
        });
    }

    private void AppDispatcherUnhandledException(object o, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);

        SaveRecover( );

        ShowException(e.Exception, "程序即将关闭。错误日志已记录。墨迹已备份。");

        Current.Shutdown(1);
    }

    private void AppStartup(object o, StartupEventArgs e)
    {
        if (!this.InitializeAsFirstInstance("LightBoardInstanceInvariantVersion"))
        {
            // 已有实例接管：文件参数已由 SignalFirstInstance 发布给第一实例。TinyIpc 的
            // PublishAsync 是 Task.Run 后台写共享内存且任务被丢弃，第二实例必须存活到写入
            // 完成，否则消息丢失、文件打不开——主窗口由 StartupUri 照常构造，恰为写入留出
            // 时间窗，不要在此提前退出或清 StartupUri。只清掉待打开文件，避免构造中读文件。
            PendingOpen = null;
            Current.Shutdown( );
            return;
        }

        recoverTimer.Interval = TimeSpan.FromMinutes(1);
        recoverTimer.Tick += (_, _) => SaveRecoverInBackground( );
        recoverTimer.Start( );
    }
}
