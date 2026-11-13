using System.Threading;

using PDFiumCore;

namespace LightBoard;

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
