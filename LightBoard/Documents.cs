using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using LightBoard.Raster;

namespace LightBoard;

public partial class MainWindow
{
    private static bool IsDocumentFile(string fileName)
    {
        return Path.GetExtension(fileName).ToUpperInvariant( ) is ".PPTX" or ".PPT" or ".DOCX" or ".DOC";
    }

    private async Task LoadImageAsync(int index)
    {
        ImageSource? image = null;
        string? error = null;

        try
        {
            image = await App.Raster!.GetOrRenderAsync(index);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (App.PageIndex != index)
        {
            return;
        }

        await Dispatcher.InvokeAsync(( ) =>
            CanvasNext.SetImage(error is null ? image : Image.Error(error))
        );

        LoadingBorder.Visibility = Visibility.Hidden;
        CanvasNext.IsEnabled = true;

        await App.Raster!.PrefetchAsync(index + 1);
        await App.Raster!.PrefetchAsync(index - 1);
    }
}

public partial class App
{
    public static RasterService? Raster { get; private set; }

    public static async Task OpenDocument(string path, IProgress<(int done, int total)>? progress = null)
    {
        Raster?.Dispose( );
        var raster = new RasterService( );

        try
        {
            await raster.OpenAsync(path, progress);
        }
        catch (Exception ex)
        {
            raster.Dispose( );

            Current.Dispatcher.Invoke(( ) =>
            {
                LogException(ex);
                ShowInfo("无法打开文档", ex.Message);
            });
            return;
        }

        Raster = raster;

        Current.Dispatcher.Invoke(( ) =>
        {
            Pages.Clear( );
            for (var i = 0; i < raster.PageCount; i++)
            {
                Pages.Add(new Page
                {
                    Number = i + 1,
                    Scale = 1.0,
                    OffsetX = 16384 - SystemParameters.WorkArea.Width / 2,
                    OffsetY = 8192 - SystemParameters.WorkArea.Height / 2,
                });
            }

            PageIndex = 0;
        });
    }
}
