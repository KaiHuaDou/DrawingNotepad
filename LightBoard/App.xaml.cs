using System;
using System.Collections.ObjectModel;
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

    public static event EventHandler? PageChanged;

    /// <summary>
    /// 板子内容（墨迹或页集合）变化的版本号；自动备份据此跳过内容未变化的分钟。
    /// </summary>
    internal static int BoardRevision { get; private set; }

    /// <summary>
    /// 已写入备份的内容版本；等于 <see cref="BoardRevision"/> 时本次备份跳过。
    /// </summary>
    private static int RecoverRevision { get; set; }

    internal static void MarkBoardChanged( )
    {
        BoardRevision++;
    }

    public static Page CurrentPage => Pages[PageIndex];

    public static int PageIndex
    {

        get;
        private set
        {
            field = value;
            PageChanged?.Invoke(Current.MainWindow, EventArgs.Empty);
        }
    } = -1;

    public static ObservableCollection<Page> Pages { get; } = [];

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
            Current.Shutdown( );
        }

        recoverTimer.Interval = TimeSpan.FromMinutes(1);
        recoverTimer.Tick += (_, _) => SaveRecoverInBackground( );
        recoverTimer.Start( );
    }
}
