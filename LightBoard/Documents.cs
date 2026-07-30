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
        ImageSource? image;
        try
        {
            image = await App.Raster!.GetOrRenderAsync(index);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            image = Image.Error(ex.Message);
        }

        if (App.PageIndex != index)
        {
            return;
        }

        await Dispatcher.InvokeAsync(( ) => CanvasNext.SetImage(image));

        LoadingBorder.Visibility = Visibility.Hidden;
        CanvasNext.IsEnabled = true;

        await App.Raster!.PrefetchAsync(index - 1);
        await App.Raster!.PrefetchAsync(index + 1);
    }
}

public partial class App
{
    public static RasterService? Raster { get; private set; }

    public static async Task OpenDocument(string path)
    {
        Raster?.Dispose( );
        Raster = new RasterService( );

        RasterSession? session = null;
        try
        {
            session = await Raster.OpenAsync(path);
            if (!session.IsPageCountReady)
            {
                await session.PageCountReady;
            }
        }
        catch (OperationCanceledException)
        {
            Raster.Dispose( );
            Raster = null;
            return;
        }
        catch (Exception ex)
        {
            Raster.Dispose( );
            Raster = null;

            Current.Dispatcher.Invoke(( ) =>
            {
                LogException(ex);
                ShowException(ex, "无法打开文档");
            });
            return;
        }

        Current.Dispatcher.Invoke(( ) =>
        {
            Pages.Clear( );
            for (var i = 0; i < session.PageCount; i++)
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
