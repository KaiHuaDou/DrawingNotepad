using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

using SingleInstanceCore;

namespace LightBoard;

public partial class App : Application, ISingleInstance
{
    public static readonly string AppPath = Path.GetDirectoryName(Environment.ProcessPath);
    public static string PendingOpen { get; set; }

    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if(args.Length > 0)
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
    }

    public void OnInstanceInvoked(string[] args)
    {
        Current.MainWindow.Show( );
        Current.MainWindow.Activate( );
    }

    private void AppDispatcherUnhandledException(object o, DispatcherUnhandledExceptionEventArgs e)
    {
        (Current.MainWindow as MainWindow).SaveStrokes(Path.Join(AppPath, DateTime.Now.Ticks.ToString( )));
        File.AppendAllText(Path.Join(AppPath, "error.log"), $"\n{e.Exception.Message}\n{e.Exception.StackTrace}\n");
        MessageBox.Show(
            "程序出现致命错误，即将关闭。错误日志已记录。墨迹已备份。",
            "轻白板",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
        Application.Current.Shutdown(1);
    }

    private void AppExit(object o, ExitEventArgs e)
    {
        // Preserved.
    }
}
