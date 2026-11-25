using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LightBoard;

// 导出菜单：收集参数后按格式分派
public partial class MainWindow
{
    private void ExportClick(object o, RoutedEventArgs e)
    {
        if (BoardState.IsBoardEmpty( ))
        {
            App.ShowInfo("没有可以导出的内容");
            return;
        }

        var window = new ExportWindow { Owner = this };
        if (window.ShowDialog( ) != true)
        {
            return;
        }

        RunExport(window.Options);
    }

    private void RunExport(ExportOptions options)
    {
        var dpi = VisualTreeHelper.GetDpi(CanvasNext);
        var progress = ShowProgress( );

        ExportMenu.IsEnabled = false;
        CanvasNext.IsEnabled = false;

        Task.Run(( ) =>
        {
            try
            {
                switch (options.Format)
                {
                    case ExportFormat.Png:
                        ImageExport.ExportAll(options.Scale, options.Path, dpi, progress);
                        break;
                    case ExportFormat.Pdf:
                        PdfExport.ExportAll(options.Path, dpi, options.Scale, progress);
                        break;
                    case ExportFormat.Svg:
                        SvgExport.ExportAll(options.Path, options.Scale, progress);
                        break;
                }
            }
            catch (Exception ex)
            {
                App.LogException(ex);
                App.ShowException(ex, "导出失败。错误日志已记录。");
                return;
            }
            finally
            {
                Dispatcher.Invoke(( ) =>
                {
                    ExportMenu.IsEnabled = true;
                    CanvasNext.IsEnabled = true;
                    HideLoading( );
                });
            }

            App.ShowInfo(CompletedTip(options.Format));
        });
    }

    private static string CompletedTip(ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Png => "导出图片成功",
            ExportFormat.Pdf => "导出 PDF 成功",
            _ => "导出 SVG 成功",
        };
    }
}
