using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace LightBoard.Tests.Toolbar;

/// <summary>
/// 单例 STA 应用线程：创建唯一 App 并常驻 Dispatcher。
/// WPF 对象具有线程亲和性且一个 AppDomain 只允许一个 Application，因此全部测试在该线程上顺序执行。
/// </summary>
internal static class UiThread
{
    private static readonly Lock Gate = new( );
    private static Dispatcher? Dispatcher;
    private static Exception? StartupError;

    private static Dispatcher AppDispatcher
    {
        get
        {
            lock (Gate)
            {
                return Dispatcher ??= Start( );
            }
        }
    }

    private static Dispatcher Start( )
    {
        var started = new ManualResetEventSlim( );
        var thread = new Thread(( ) =>
        {
            try
            {
                App app = new( )
                {
                    // 测试环境不调用 InitializeComponent：XAML 绑定的 StartupUri 会自动建窗、AppStartup 会执行
                    // 单实例互斥并启动恢复定时器，均不适用于测试。这里手动合并 App.xaml 中声明的主题资源，
                    // 供 MainWindow 解析其中的资源键。
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                var resources = app.Resources;
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/PresentationFramework.Aero;component/themes/Aero.NormalColor.xaml", UriKind.Absolute)
                });
                resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/LightBoard;component/Theme.xaml", UriKind.Absolute)
                });
                Dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                StartupError = ex;
            }
            finally
            {
                started.Set( );
            }

            Dispatcher.Run( );
        })
        {
            Name = "ToolbarTestApp",
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start( );
        started.Wait( );
        if (StartupError is not null)
        {
            ExceptionDispatchInfo.Capture(StartupError).Throw( );
        }

        return Dispatcher!;
    }

    /// <summary>在唯一 App 的 STA 线程上执行测试体，异常原样抛出。</summary>
    public static void Run(Action action)
    {
        Exception? error = null;
        AppDispatcher.Invoke(( ) =>
        {
            try
            {
                action( );
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw( );
        }
    }
}
