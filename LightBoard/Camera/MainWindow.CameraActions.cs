#if OPENCV
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
#endif

namespace LightBoard;

// 展台覆盖层的按钮处理器与设备开合
public partial class MainWindow
{
#if OPENCV
    private async void CameraSwitchClick(object o, RoutedEventArgs e)
    {
        if (camera is null || cameraOpening)
        {
            return;
        }

        cameraGeneration++;
        var generation = cameraGeneration;
        cameraOpening = true;
        SetCameraButtonsEnabled(false);
        ShowCameraTip("正在切换设备……");

        try
        {
            var devices = await Task.Run(( ) => CameraDevices.Enumerate(cameraDeviceIndex + 1));
            var opened = devices.Count > 0
                && await OpenCameraAsync(CameraDevices.Resolve(cameraDeviceIndex + 1, devices.Count), devices, generation);
            if (generation != cameraGeneration)
            {
                return;
            }

            if (opened)
            {
                HideCameraTip( );
            }
            else
            {
                ShowCameraTip("无法打开摄像头设备", autoHide: true);
            }
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

    private void CameraRotateClick(object o, RoutedEventArgs e)
    {
        cameraAngle = (cameraAngle + 90) % 360;

        // 位图的宽高随取向交换；取向先推进采集线程，画面随下一帧跟上，冻结帧立即重写
        if (camera is not null)
        {
            var (width, height) = CameraOrientation.PixelSize(camera.FrameWidth, camera.FrameHeight, cameraAngle);
            cameraBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
            camera.SetOrientation(cameraAngle, CameraMirrorButton.IsChecked == true);
            BlitCamera( );
            RefreshVideoBackground( );
        }
    }

    // 冻结期间画面停在冻结帧上，镜像要在这一刻重写位图才生效；未冻结时采集线程从下一帧带上镜像
    private void CameraMirrorClick(object o, RoutedEventArgs e)
    {
        camera?.SetOrientation(cameraAngle, CameraMirrorButton.IsChecked == true);

        if (CameraFreezeButton.IsChecked == true)
        {
            BlitCamera( );
        }
    }

    private void CameraFreezeClick(object o, RoutedEventArgs e)
    {
        if (o is not ToggleButton button)
        {
            return;
        }

        SetCameraFreeze(button.IsChecked == true);
    }

    private async void CameraPhotoClick(object o, RoutedEventArgs e)
    {
        if (camera is null)
        {
            return;
        }

        // UI 状态先在 UI 线程取值，线程池里不碰依赖对象
        var capture = camera;
        var angle = cameraAngle;
        var mirror = CameraMirrorButton.IsChecked == true;
        // 拍照期间可能翻页或退出展台：照片只落回按下拍照那一刻的视频页
        var target = videoPage;

        CameraPhotoButton.IsEnabled = false;
        ShowCameraTip("正在拍照……");

        (string PhotoPath, BitmapSource Photo)? photo;
        try
        {
            photo = await Task.Run(( ) => CameraPhoto.Take(capture, angle, mirror));
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            photo = null;
        }

        var frozen = photo is not null && FreezeVideoPage(target, photo.Value.PhotoPath, photo.Value.Photo);
        // 期间可能已开始换设备或退出展台，按钮可用性按当时的状态还原
        CameraPhotoButton.IsEnabled = !cameraOpening;
        if (frozen)
        {
            HideCameraTip( );
        }
        else
        {
            ShowCameraTip("拍照失败", autoHide: true);
        }
    }

    private void CameraCloseClick(object o, RoutedEventArgs e)
    {
        ReturnToGlobalBackground( );
    }

    // 冻结期间不向位图写帧，画面与拍照都停在冻结那一刻；新开的设备从解冻开始，残留的勾选会让预览一直空着
    private void SetCameraFreeze(bool frozen)
    {
        CameraFreezeButton.IsChecked = frozen;
        CameraFreezeButton.Tag = frozen ? "\uE768" : "\uE769";

        if (camera is null || cameraBitmap is null)
        {
            return;
        }

        if (frozen)
        {
            camera.Freeze( );
            // 画面停在冻结帧上，否则最后一帧之前的内容与拍照结果不一致
            BlitCamera( );
        }
        else
        {
            camera.Unfreeze( );
        }
    }

    // 换设备共用：新设备开成功后才换下旧实例，打开失败时原设备继续出帧。
    // 打开期间退出展台（代次变化）时，迟到的结果就地销毁
    private async Task<bool> OpenCameraAsync(int index, List<CameraDevice> devices, int generation)
    {
        var device = devices[index];
        var capture = new CameraCapture( );
        try
        {
            if (!await capture.OpenAsync(device.Index, device.Format) || generation != cameraGeneration)
            {
                capture.Dispose( );
                return false;
            }
        }
        catch (Exception e)
        {
            App.LogException(e);
            capture.Dispose( );
            return false;
        }

        // 位图按新协商尺寸与当前取向重建，分配先于换下旧实例，失败时原设备不受影响
        var (width, height) = CameraOrientation.PixelSize(capture.FrameWidth, capture.FrameHeight, cameraAngle);
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
        capture.Stopped += OnCameraStopped;
        capture.SetOrientation(cameraAngle, CameraMirrorButton.IsChecked == true);

        UnsubscribeCameraRendering( );
        camera?.Dispose( );
        camera = capture;
        cameraBitmap = bitmap;
        cameraFrameId = -1;
        cameraDeviceIndex = index;
        CameraDeviceText.Text = DeviceLabel(device.Name, capture);
        SetCameraFreeze(false);
        SubscribeCameraRendering( );
        return true;
    }

    // 设备名后带上协商结果，实际拿到的档与设备最大档不一致时一眼能看出
    private static string DeviceLabel(string name, CameraCapture capture)
    {
        return capture.FrameRate > 0
            ? $"{name}  {capture.FrameWidth}×{capture.FrameHeight} {capture.FrameRate:0}fps"
            : $"{name}  {capture.FrameWidth}×{capture.FrameHeight}";
    }

    // 拍照翻页：当前帧冻结为视频页的静态背景，新增页并切换过去，视频背景接续在新页上。
    // 拍照只动背景与新页，不算未保存的墨迹
    private bool FreezeVideoPage(Page? target, string photoPath, BitmapSource photo)
    {
        if (target is null || videoArea.IsEmpty)
        {
            return false;
        }

        // 位图随拍照带回：登记后画布、缩略图与导出直接取内存位图，路径供保存与下次打开解码
        App.RegisterPhotoSource(photoPath, photo);

        // 照片与原视频画面重合在同一块内容区域
        target.Background = new DocumentBackground(photoPath, 1, videoArea);
        target.InvalidatePreview( );

        // 新页沿用拍照时的缩放与平移；视频区域不随视口重算，画面在内容坐标里原地不动
        var view = CanvasNext.CurrentView;
        var page = new Page
        {
            Number = App.Pages.Count + 1,
            Scale = view.Scale,
            OffsetX = view.OffsetX,
            OffsetY = view.OffsetY,
        };
        App.Pages.Add(page);
        videoPage = page;
        App.SwitchPage(App.Pages.Count - 1);
        App.BoardRevision++;
        return true;
    }
#endif
}
