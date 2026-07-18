using System;
using System.Windows;
using System.Windows.Threading;

using SingleInstanceCore;

namespace LightBoard;

public partial class App : Application, ISingleInstance
{
    public static class Program
    {
        [STAThread]
        public static void Main( )
        {
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
        // TODO: Save Data.
    }

    private void AppExit(object sender, ExitEventArgs e)
    {
        // TODO: Save Data.
    }
}
