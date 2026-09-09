using System.Runtime.ExceptionServices;

namespace InkCanvasNext.Tests;

/// <summary>在独立 STA 线程上执行测试体（WPF 控件与触摸输入管线要求 STA）。</summary>
internal static class StaTest
{
    public static void Run(Action action)
    {
        Exception? error = null;

        var thread = new Thread(( ) =>
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

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start( );
        thread.Join( );

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw( );
        }
    }
}
