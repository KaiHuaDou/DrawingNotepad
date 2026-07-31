using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using Ookii.Dialogs.Wpf;

using SingleInstanceCore;

using Syncfusion.Licensing;

namespace LightBoard;

public partial class App : Application, ISingleInstance
{
    public static readonly string AppPath = Path.GetDirectoryName(Environment.ProcessPath)!;

    private readonly DispatcherTimer recoverTimer = new( );

    public static event EventHandler? PageChanged;

    public static Page CurrentPage => Pages[PageIndex];

    public static int PageIndex
    {

        get => field;
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
            RegisterSyncfusionLicense( );

            if (args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                PendingOpen = args[0];
            }

            App app = new( );
            app.InitializeComponent( );
            app.Run( );
            SingleInstance.Cleanup( );
        }

        private static void RegisterSyncfusionLicense( )
        {
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE");

            if (string.IsNullOrWhiteSpace(key))
            {
                var licenseFile = Path.Join(AppPath, "syncfusion.license");
                if (File.Exists(licenseFile))
                {
                    key = File.ReadAllText(licenseFile).Trim( );
                }
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                SyncfusionLicenseProvider.RegisterLicense(key);
            }
        }
    }

    public static void LogException(Exception e)
    {
        File.AppendAllText(Path.Join(AppPath, "error.log"), $"\nTime:{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{e.Message}\n{e.StackTrace}\n");
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
            try { Clipboard.SetDataObject(details, true); } catch { }
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

    public void OnInstanceInvoked(string[] args)
    {
        Current.MainWindow.Show( );
        Current.MainWindow.Activate( );
    }

    private void AppDispatcherUnhandledException(object o, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception);

        try { SaveRecover( ); } catch { }

        ShowException(e.Exception, "程序即将关闭。错误日志已记录。墨迹已备份。");

        Application.Current.Shutdown(1);
    }

    private void AppStartup(object o, StartupEventArgs e)
    {
        if (!this.InitializeAsFirstInstance("LightBoardInstanceInvariantVersion"))
        {
            Current.Shutdown( );
        }

        recoverTimer.Interval = TimeSpan.FromMinutes(1);
        recoverTimer.Tick += (_, _) => SaveRecover( );
        recoverTimer.Start( );
    }

    private static void SaveRecover( )
    {
        if (!Pages.Any(p => p.Strokes.Count > 0))
        {
            return;
        }

        try
        {
            BoardFile.Write(Path.Join(AppPath, "recover", $"{DateTime.Now:yyyyMMdd-HHmmss}"), Pages);
        }
        catch { }
    }
}
