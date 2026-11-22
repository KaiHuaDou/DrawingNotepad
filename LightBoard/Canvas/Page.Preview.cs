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
    /// 页面缩略图；尚未生成时为占位图
    /// 过期时在后台重建，完成后触发绑定刷新
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

    internal bool PreviewStale { get; private set; }

    /// <summary>
    /// 标记缩略图过期：墨迹或文档背景变化后调用，等下一次读取或显式刷新时重建
    /// </summary>
    internal void InvalidatePreview( )
    {
        previewVersion++;
        PreviewStale = true;
    }

    /// <summary>
    /// 过期时重建缩略图；同一页同时只跑一个
    /// 取快照推到布局之后：绑定在布局过程中读 Preview，整页克隆压在布局里会挡住页面列表动画
    /// </summary>
    internal void RefreshPreview( )
    {
        if (!PreviewStale || previewRendering)
        {
            return;
        }

        previewRendering = true;
        var version = previewVersion;

        // 取应用 Dispatcher：CurrentDispatcher 在非 UI 线程首次调用会新建一个不泵送的实例
        Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            ( ) => StartPreviewRender(version)
        );
    }

    private void StartPreviewRender(int version)
    {
        var strokes = new StrokeCollection([.. Strokes.Select(s => s.Clone( ))]);
        var background = Background;
        var image = background is null ? null : DocumentRegistry.Image(background);
        var box = background?.Box;
        var canvas = BoardState.Underlay.ToColor( );

        Task.Run(( ) =>
        {
            ImageSource? rendered = null;
            try
            {
                rendered = strokes.Preview(image, box, canvas);
            }
            catch (Exception e)
            {
                App.LogException(e);
            }

            // 失败时同样要复位 previewRendering，否则这一页的缩略图此后再无重建机会
            // 回填会触发列表项重新布局，让输入与渲染先走
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, ( ) =>
            {
                try
                {
                    if (rendered is not null)
                    {
                        Preview = rendered;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
                    }
                }
                finally
                {
                    previewRendering = false;
                    PreviewStale = previewVersion != version;
                }
            });
        });
    }
}
