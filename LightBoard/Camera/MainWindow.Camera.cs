#if OPENCV
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
#endif

namespace LightBoard;

public partial class MainWindow
{
#if OPENCV
    private CameraCapture? camera;
    private WriteableBitmap? cameraBitmap;
    private int cameraFrameId = -1;
    private int cameraAngle;
    private int cameraDeviceIndex;
    private bool cameraSubscribed;

    // 视频背景仅在至多一页上生效；拍照时该页冻结为照片背景，新页接续视频
    private Page? videoPage;

    // 视频画面在内容坐标中的区域，进入视频背景时取当前视口；画面随之随画布缩放平移
    private Rect videoArea;

    // 最近勾选的全局背景（网格 / 纯白 / 纯黑 / 透明），离开视频页与退出展台时恢复勾选。
    // 组互斥在 Click 之前就取消了其余单选，所以只能在点击处记录，不能事后读 IsChecked
    private RadioButton? globalBackgroundRadio;

    // 打开可能很慢：cameraOpening 期间禁用按钮；cameraGeneration 让切换走之后的迟到结果就地销毁
    private bool cameraOpening;
    private int cameraGeneration;

    // 展台提示文字；null 即不显示。显隐由 RefreshVideoBackground 按当前页决定，翻页时联动
    private string? cameraTip;
    private readonly DispatcherTimer cameraTipTimer = new( )
    {
        Interval = TimeSpan.FromSeconds(2.5)
    };

    // 覆盖层按钮不经 XAML 接线：nocv 构建里这些处理器不存在
    private void WireCameraOverlay( )
    {
        CameraSwitchButton.Click += CameraSwitchClick;
        CameraRotateButton.Click += CameraRotateClick;
        CameraMirrorButton.Click += CameraMirrorClick;
        CameraFreezeButton.Click += CameraFreezeClick;
        CameraPhotoButton.Click += CameraPhotoClick;
        CameraCloseButton.Click += CameraCloseClick;
        cameraTipTimer.Tick += (_, _) => HideCameraTip( );
    }

    // 提示跟随视频页显隐；autoHide 的提示数秒后自动消失
    private void ShowCameraTip(string text, bool autoHide = false)
    {
        cameraTip = text;
        if (autoHide)
        {
            cameraTipTimer.Stop( );
            cameraTipTimer.Start( );
        }
        else
        {
            cameraTipTimer.Stop( );
        }

        RefreshVideoBackground( );
    }

    private void HideCameraTip( )
    {
        cameraTip = null;
        cameraTipTimer.Stop( );
        RefreshVideoBackground( );
    }

    // 视频背景仅在视频页上显示；翻页、换背景或清板时同步背景页、提示与背景单选
    private void RefreshVideoBackground( )
    {
        if (videoPage is not null && !App.Pages.Contains(videoPage))
        {
            // 页面被清空或移除：视频背景随之消失
            videoPage = null;
            StopVideoStand( );
        }

        var onVideoPage = videoPage is not null && ReferenceEquals(App.CurrentPage, videoPage);
        CameraBar.Visibility = onVideoPage ? Visibility.Visible : Visibility.Collapsed;
        CameraTipText.Text = cameraTip ?? "";
        CameraTipText.Visibility = onVideoPage && cameraTip is not null ? Visibility.Visible : Visibility.Collapsed;
        CanvasNext.Background = BackgroundBrush( );

        // 视频页让位页自身的文档背景：视频画面占住画布内的背景位
        var page = App.CurrentPage;
        CanvasNext.SetDocumentPage(
            onVideoPage ? cameraBitmap : App.BackgroundImage(page.Background),
            onVideoPage ? videoArea : page.Background?.Box);

        // 背景单选跟随当前页：视频页勾「视频」，其余页回到最近勾选的全局背景
        VideoBackgroundRadio.IsChecked = onVideoPage;
        if (!onVideoPage)
        {
            (globalBackgroundRadio ?? DefaultBackgroundRadio).IsChecked = true;
        }
    }

    private async void StartVideoStand( )
    {
        if (cameraOpening)
        {
            return;
        }

        // 至多一页：视频背景随当前页走，换页时画面重新落在新页的视口内
        if (!ReferenceEquals(videoPage, App.CurrentPage))
        {
            videoPage = App.CurrentPage;
            videoArea = App.CanvasView( ).Box;
        }

        UpdateCameraBarMargin( );
        RefreshVideoBackground( );

        if (camera is not null)
        {
            return;   // 相机已在运行，仅把视频背景挪到当前页
        }

        cameraGeneration++;
        var generation = cameraGeneration;
        cameraOpening = true;
        SetCameraButtonsEnabled(false);
        ShowCameraTip("正在连接设备……");

        try
        {
            var devices = await Task.Run(( ) => CameraDevices.Enumerate(cameraDeviceIndex));
            // 无设备时没有下标可取，短路在 && 左侧
            var opened = devices.Count > 0
                && await OpenCameraAsync(CameraDevices.Resolve(cameraDeviceIndex, devices.Count), devices, generation);
            if (generation != cameraGeneration)
            {
                return;   // 期间已切走，界面归新会话管
            }

            if (opened)
            {
                HideCameraTip( );
                return;
            }

            // 打不开：回退进入前的背景并提示
            ReturnToGlobalBackground( );
            App.ShowInfo(devices.Count == 0 ? "未找到摄像头设备" : "无法打开摄像头设备");
        }
        finally
        {
            if (generation == cameraGeneration)
            {
                cameraOpening = false;
                SetCameraButtonsEnabled(true);
            }
        }
    }

    private void StopVideoStand( )
    {
        cameraGeneration++;
        cameraOpening = false;
        UnsubscribeCameraRendering( );
        camera?.Dispose( );
        camera = null;
        cameraBitmap = null;
        cameraFrameId = -1;
        videoArea = Rect.Empty;
        CameraBar.Visibility = Visibility.Collapsed;
        cameraTip = null;
        SetCameraButtonsEnabled(true);
    }

    // 离开视频背景：停采集并把背景单选还给最近勾选的全局背景
    private void ReturnToGlobalBackground( )
    {
        videoPage = null;
        StopVideoStand( );
        (globalBackgroundRadio ?? DefaultBackgroundRadio).IsChecked = true;
        ApplyBackground( );
    }

    // 展台工具栏恰在 CenterBorder 上方：两者都不遮挡，随 CenterBorder 折叠位移与倍率一起移动
    private void UpdateCameraBarMargin( )
    {
        CameraBar.Margin = new Thickness(0, 0, 0, EdgeHintGap(CenterBorder.ActualHeight, CenterBorder.Margin.Bottom, toolbarScale));
    }

    // 打开期间不可用的按钮统一开关；关闭按钮始终可用
    private void SetCameraButtonsEnabled(bool enabled)
    {
        CameraSwitchButton.IsEnabled = enabled;
        CameraRotateButton.IsEnabled = enabled;
        CameraMirrorButton.IsEnabled = enabled;
        CameraFreezeButton.IsEnabled = enabled;
        CameraPhotoButton.IsEnabled = enabled;
    }

    // 立即把当前帧按预览取向写进位图；冻结中换取向用它让画面马上跟上
    private void BlitCamera( )
    {
        if (camera is not null && cameraBitmap is not null)
        {
            camera.BlitLatestTo(cameraBitmap, cameraAngle, CameraMirrorButton.IsChecked == true);
        }
    }

    // 渲染节拍写入最新帧；冻结、不在视频页或帧号未变时跳过（源 30 fps，显示器刷新率更高）
    private void CameraFrame(object? o, EventArgs e)
    {
        if (camera is null
            || cameraBitmap is null
            || CameraFreezeButton.IsChecked == true
            || !ReferenceEquals(App.CurrentPage, videoPage))
        {
            return;
        }

        var id = camera.FrameId;
        if (id == cameraFrameId)
        {
            return;
        }

        cameraFrameId = id;
        BlitCamera( );
    }

    private void OnCameraStopped( )
    {
        Dispatcher.BeginInvoke(( ) =>
        {
            // 主动停机时采集线程同样会退出，迟到的通知不能再动界面
            if (camera is null)
            {
                return;
            }

            ReturnToGlobalBackground( );
            App.ShowInfo("摄像头已断开");
        });
    }

    private void SubscribeCameraRendering( )
    {
        if (cameraSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering += CameraFrame;
        cameraSubscribed = true;
    }

    private void UnsubscribeCameraRendering( )
    {
        if (!cameraSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= CameraFrame;
        cameraSubscribed = false;
    }
#endif
}
