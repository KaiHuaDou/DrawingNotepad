using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

/// <summary>
/// 图片导出：每页渲染为 PNG
/// </summary>
internal static class ImageExport
{
    public static void ExportAll(int scale, string directory, DpiScale dpi, IProgress<ExportProgress>? progress = null)
    {
        const string tip = "正在导出图片";

        Directory.CreateDirectory(directory);

        // 导出在后台线程进行，页集合在 UI 线程取快照
        var pages = Application.Current.Dispatcher.Invoke(( ) => BoardState.Pages.ToArray( ));
        var count = pages.Length;
        var pad = (int) (Math.Log10(count) + 1);
        progress?.Report(new(tip, 0, count));

        for (var i = 0; i < count; i++)
        {
            var snapshot = Application.Current.Dispatcher.Invoke(( ) => PageSnapshot.Capture(pages[i]));
            var fileName = Path.Join(directory, $"{snapshot.Number.ToString( ).PadLeft(pad, '0')}.png");

            snapshot.Strokes.ExportPng(
                fileName,
                dpi,
                scale,
                canvas: snapshot.Canvas,
                background: snapshot.Background,
                box: snapshot.Box);
            progress?.Report(new(tip, i + 1, count));
        }
    }
}
