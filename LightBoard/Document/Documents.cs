using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

using InkCanvasNext;

namespace LightBoard;

public partial class App
{
    public static DocumentService? Document { get; internal set; }

    // 文档背景页在内容坐标中的 Uniform 适配盒子（打开/附加文档时画布的可见内容区域）；显示、缩略图与导出共用
    internal static Rect? DocumentBox { get; set; }

    public static async Task OpenDocument(string path)
    {
        var document = await DocumentService.OpenAsync(path);

        if (document.PageCount == 0)
        {
            document.Dispose( );
            throw new InvalidDataException("文档没有任何页面");
        }

        Pages.Clear( );

        var (scale, offsetX, offsetY, box) = CanvasView( );

        for (var i = 0; i < document.PageCount; i++)
        {
            AddDocumentPage(scale, offsetX, offsetY);
        }

        Document = document;
        DocumentBox = box;
        PageIndex = 0;
    }

    private static void AddDocumentPage(double scale, double offsetX, double offsetY)
    {
        var page = new Page
        {
            Number = Pages.Count + 1,
            Scale = scale,
            OffsetX = offsetX,
            OffsetY = offsetY,
        };

        // 文档页即使没有墨迹也有背景缩略图
        page.InvalidatePreview( );
        Pages.Add(page);
    }

    public static async Task<DocumentService> AttachDocument(string path)
    {
        var document = await DocumentService.OpenAsync(path);

        if (document.PageCount == 0)
        {
            document.Dispose( );
            throw new InvalidDataException("文档没有任何页面");
        }

        var (scale, offsetX, offsetY, box) = CanvasView( );

        while (Pages.Count < document.PageCount)
        {
            AddDocumentPage(scale, offsetX, offsetY);
        }

        // 当前页视图同步为捕获视图（即画布当前视图），换页往返后文档盒仍落在视口内
        var current = CurrentPage;
        current.Scale = scale;
        current.OffsetX = offsetX;
        current.OffsetY = offsetY;

        Document = document;
        DocumentBox = box;
        return document;
    }

    // 捕获画布当前视图：文档盒取可见内容区域，背景随之落在视口内、画布无需移动；各文档页视图取捕获视图，
    // 换页后外观一致。极小缩放下可见区域大于画布，盒子夹回画布内。窗口尚未完成布局（如带参数启动即打开文档）
    // 时视口尺寸未知，按 1x 整块主屏估算（无边框最大化窗口的视口即整块主屏）。
    private static (double Scale, double OffsetX, double OffsetY, Rect Box) CanvasView( )
    {
        var canvas = (Current.MainWindow as MainWindow)?.CanvasNext;

        if (canvas is null || canvas.ViewportSize is not { Width: > 0, Height: > 0 } viewport)
        {
            var width = SystemParameters.PrimaryScreenWidth;
            var height = SystemParameters.PrimaryScreenHeight;
            return (1.0, InitialOffset.X, InitialOffset.Y, new Rect(InitialOffset.X, InitialOffset.Y, width, height));
        }

        var scale = canvas.CurrentScale;
        var offsetX = canvas.OffsetX;
        var offsetY = canvas.OffsetY;

        var box = new Rect(
            offsetX / scale,
            offsetY / scale,
            viewport.Width / scale,
            viewport.Height / scale);
        box.Intersect(new Rect(0, 0, CanvasSize.Width, CanvasSize.Height));

        return (scale, offsetX, offsetY, box);
    }
}
