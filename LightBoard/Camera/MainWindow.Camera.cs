#if OPENCV
using System;
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

    // 打开可能很慢：cameraOpening 期间禁用按钮；cameraGeneration 让切换走之后的迟到结果就地销毁
    private bool cameraOpening;
    private int cameraGeneration;
    private bool cameraFrozen;

    // 展台提示文字；null 即不显示。显隐由 SyncVideoOverlay 按当前页决定，翻页时联动
    private string? cameraTip;
    private readonly DispatcherTimer cameraTipTimer = new( )
    {
        Interval = TimeSpan.FromSeconds(2.5)
    };

    // 覆盖层按钮不经 XAML 接线：nocv 构建里这些处理器不存在
    private void WireCameraOverlay( )
    {
        VideoMenuItem.Click += VideoMenuItemClick;
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

        SyncVideoOverlay( );
    }

    private void HideCameraTip( )
    {
        cameraTip = null;
        cameraTipTimer.Stop( );
        SyncVideoOverlay( );
    }

    // 视频层只随当前页走；翻页、换背景或清板时同步背景页、提示与菜单勾选
    private void SyncVideoOverlay( )
    {
        if (videoPage is not null && !App.Pages.Contains(videoPage))
        {
            // 页面被清空或移除：视频背景随之消失
            videoPage = null;
            StopVideoStand( );
        }

        var onVideoPage = videoPage is not null && ReferenceEquals(App.CurrentPage, videoPage);

        // 渲染节拍只在视频页订阅：其余页相机继续采集，但不驱动界面重绘
        if (onVideoPage && camera is not null)
        {
            SubscribeCameraRendering( );
        }
        else
        {
            UnsubscribeCameraRendering( );
        }

        CameraBar.Visibility = onVideoPage ? Visibility.Visible : Visibility.Collapsed;
        CameraTipText.Text = cameraTip ?? "";
        CameraTipText.Visibility = onVideoPage && cameraTip is not null ? Visibility.Visible : Visibility.Collapsed;

        // 视频页让位页自身的文档背景：视频画面占住画布内的背景位
        var page = App.CurrentPage;
        CanvasNext.SetDocumentPage(
            onVideoPage ? cameraBitmap : App.BackgroundImage(page.Background),
            onVideoPage ? videoArea : page.Background?.Box);

        // 菜单勾选表示展台开着，与当前页无关
        VideoMenuItem.IsChecked = videoPage is not null;
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
        SyncVideoOverlay( );

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
            StopVideo( );
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

    // 离开展台：停采集并解绑视频页
    private void StopVideo( )
    {
        videoPage = null;
        StopVideoStand( );
        VideoMenuItem.IsChecked = false;
        SyncVideoOverlay( );
    }

    // 更多菜单里的展台开关：勾上开展台，取消关展台
    private void VideoMenuItemClick(object o, RoutedEventArgs e)
    {
        if (o is not MenuItem { IsChecked: bool isChecked })
        {
            return;
        }

        if (isChecked)
        {
            StartVideoStand( );
        }
        else
        {
            StopVideo( );
        }
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
            || cameraFrozen
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

            StopVideo( );
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
