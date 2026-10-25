using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;

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
    /// 过期时重建缩略图；同一页同时只跑一个。
    /// 取快照推到布局之后：绑定在布局过程中读 Preview，整页克隆压在布局里会挡住页面列表动画。
    /// </summary>
    internal void RefreshPreview( )
    {
        if (!PreviewStale || previewRendering)
        {
            return;
        }

        previewRendering = true;
        var version = previewVersion;

        // 快照必须由持有这些墨迹的线程取，因此排在调用方自己的 Dispatcher 上
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            ( ) => StartPreviewRender(version));
    }

    private void StartPreviewRender(int version)
    {
        // 快照必须在 UI 线程取：渲染期间画布仍可被编辑（选区变换会就地改触点）；背景图同理。
        var strokes = new StrokeCollection([.. Strokes.Select(s => s.Clone( ))]);
        var background = Background;
        var image = background is null ? null : App.BackgroundImage(background);
        var box = background?.Box;

        Task.Run(( ) =>
        {
            ImageSource? rendered = null;
            try
            {
                rendered = strokes.Preview(image, box);
            }
            catch (Exception e)
            {
                App.LogException(e);
            }

            // 失败时同样要复位 previewRendering，否则这一页的缩略图此后再无重建机会
            Application.Current.Dispatcher.BeginInvoke(( ) =>
            {
                if (rendered is not null)
                {
                    Preview = rendered;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
                }

                previewRendering = false;
                PreviewStale = previewVersion != version;
            });
        });
    }
}
