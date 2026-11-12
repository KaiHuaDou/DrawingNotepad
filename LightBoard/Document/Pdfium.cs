using System.Threading;

using PDFiumCore;

namespace LightBoard;

// PDFium 是进程级库，没有引用计数：首次使用时初始化一次，FPDF_DestroyLibrary 由进程退出回收
internal static class Pdfium
{
    private static readonly Lock gate = new( );

    private static bool initialized;

    internal static void EnsureInitialized( )
    {
        lock (gate)
        {
            if (initialized)
            {
                return;
            }

            fpdfview.FPDF_InitLibrary( );
            initialized = true;
        }
    }
}
