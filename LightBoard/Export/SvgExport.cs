using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace LightBoard;

/// <summary>
/// SVG 导出：每页渲染为一个矢量文件
/// </summary>
internal static class SvgExport
{
    public static void ExportAll(string directory, IProgress<ExportProgress>? progress = null)
    {
        const string tip = "正在导出 SVG";

        Directory.CreateDirectory(directory);

        var pages = Application.Current.Dispatcher.Invoke(( ) => BoardState.Pages.ToArray( ));
        var count = pages.Length;
        var pad = (int) (Math.Log10(count) + 1);
        progress?.Report(new(tip, 0, count));

        for (var i = 0; i < count; i++)
        {
            var snapshot = Application.Current.Dispatcher.Invoke(( ) => PageSnapshot.Capture(pages[i]));
            var fileName = Path.Join(directory, $"{snapshot.Number.ToString( ).PadLeft(pad, '0')}.svg");

            SvgWriter.Write(fileName, snapshot);
            progress?.Report(new(tip, i + 1, count));
        }
    }
}
