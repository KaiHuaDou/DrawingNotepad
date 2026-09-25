using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

using InkCanvasNext;

namespace LightBoard;

public partial class Page
{
    private bool previewRendering;
    private int previewVersion;

    /// <summary>
    /// 页面缩略图；尚未生成时为占位图。
    /// getter 有副作用：读取代表"这一页即将被看到"（页面列表的行被虚拟化实现时才读），
    /// 过期时在后台重建，完成后触发绑定刷新。
    /// </summary>
    public ImageSource Preview
    {
        get
        {
            RefreshPreview( );
            return field;
        }

        private set;
    } = StrokeCollectionExtension.PreviewEmpty( );

    /// <summary>
    /// 缩略图是否已过期。
    /// </summary>
    internal bool PreviewStale { get; private set; }

    /// <summary>
    /// 标记缩略图过期：墨迹或文档背景变化后调用，等下一次读取或显式刷新时重建。
    /// </summary>
    internal void InvalidatePreview( )
    {
        previewVersion++;
        PreviewStale = true;
    }

    /// <summary>
    /// 过期时在后台重建缩略图；同一页同时只跑一个。
    /// </summary>
    internal void RefreshPreview( )
    {
        if (!PreviewStale || previewRendering)
        {
            return;
        }

        previewRendering = true;
        var version = previewVersion;

        // 快照必须在 UI 线程取：渲染期间画布仍可被编辑（选区变换会就地改触点）。
        var strokes = new StrokeCollection([.. Strokes.Select(s => s.Clone( ))]);

        Task.Run(( ) =>
        {
            ImageSource? image = null;
            try
            {
                image = strokes.Preview(App.CanvasSize, BackgroundPage( ));
            }
            catch (Exception e)
            {
                App.LogException(e);
            }

            // 失败时同样要复位 previewRendering，否则这一页的缩略图此后再无重建机会
            Application.Current.Dispatcher.BeginInvoke(( ) =>
            {
                if (image is not null)
                {
                    Preview = image;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
                }

                previewRendering = false;
                PreviewStale = previewVersion != version;
            });
        });
    }

    /// <summary>
    /// 文档背景页：XPS 页树有线程亲和性，只能在 UI 线程取（结果已冻结，可跨线程绘制）。
    /// </summary>
    private ImageSource? BackgroundPage( )
    {
        if (App.Document is null)
        {
            return null;
        }

        return Application.Current.Dispatcher.Invoke(( ) => App.Document.GetPage(Number - 1));
    }
}
