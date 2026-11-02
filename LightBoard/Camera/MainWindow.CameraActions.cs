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
            var devices = await Task.Run(CameraDevices.Enumerate);
            var opened = devices.Count > 0
                && await OpenCameraAsync((cameraDeviceIndex + 1) % devices.Count, devices, generation);
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
        CameraRotateTransform.Angle = cameraAngle;
        UpdateCameraView( );
    }

    private void CameraMirrorClick(object o, RoutedEventArgs e)
    {
        if (o is not ToggleButton button)
        {
            return;
        }

        CameraMirrorTransform.ScaleX = button.IsChecked == true ? -1 : 1;
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

        CameraPhotoButton.IsEnabled = false;
        ShowCameraTip("正在拍照……");

        string? path;
        try
        {
            path = await Task.Run(( ) => CameraPhoto.Take(capture, angle, mirror));
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            path = null;
        }

        var frozen = path is not null && await FreezeVideoPage(path);
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
            camera.BlitLatestTo(cameraBitmap);
        }
        else
        {
            camera.Unfreeze( );
        }
    }

    // 换设备共用：新设备开成功后才换下旧实例，打开失败时原设备继续出帧。
    // 打开期间退出展台（代次变化）时，迟到的结果就地销毁
    private async Task<bool> OpenCameraAsync(int index, IReadOnlyList<CameraDevice> devices, int generation)
    {
        var device = devices[index];
        var capture = new CameraCapture( );
        try
        {
            if (!await capture.OpenAsync(device) || generation != cameraGeneration)
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

        // 位图按新协商尺寸重建，分配先于换下旧实例，失败时原设备不受影响
        var bitmap = new WriteableBitmap(capture.FrameWidth, capture.FrameHeight, 96, 96, PixelFormats.Bgr24, null);
        capture.Stopped += OnCameraStopped;

        UnsubscribeCameraRendering( );
        camera?.Dispose( );
        camera = capture;
        cameraBitmap = bitmap;
        cameraFrame = new Size(capture.FrameWidth, capture.FrameHeight);
        cameraFrameId = -1;
        cameraDeviceIndex = index;
        CameraDeviceText.Text = DeviceLabel(device.Name, capture);
        CameraImage.Source = bitmap;
        SetCameraFreeze(false);
        UpdateCameraView( );
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

    // 拍照翻页：当前帧冻结为视频页的静态背景，新增页并切换过去，视频背景接续在新页上
    private async Task<bool> FreezeVideoPage(string photoPath)
    {
        var current = videoPage;
        if (current is null)
        {
            return false;
        }

        var (scale, offsetX, offsetY, _) = App.CanvasView( );
        var box = PhotoBox(scale, offsetX, offsetY);
        if (box.IsEmpty)
        {
            return false;
        }

        // 背景按路径取图，照片必须先登记为文档源，否则画布、缩略图与导出都取不到它
        try
        {
            await App.OpenDocumentSourceAsync(photoPath);
        }
        catch (Exception e)
        {
            App.LogException(e);
            return false;
        }

        // 取图期间展台被关掉或换了视频页：这张照片不再落回任何页
        if (!ReferenceEquals(videoPage, current))
        {
            return false;
        }

        current.Background = new DocumentBackground(photoPath, 1, box);
        current.InvalidatePreview( );

        var page = new Page
        {
            Number = App.Pages.Count + 1,
            Scale = scale,
            OffsetX = offsetX,
            OffsetY = offsetY,
        };
        App.Pages.Add(page);
        videoPage = page;
        App.SwitchPage(App.Pages.Count - 1);
        App.BoardRevision++;
        Dirty = true;
        return true;
    }

    // 照片在内容坐标中的盒子：取预览画面在视口里占的区域，照片与此刻的笔迹因此落在同一位置
    private Rect PhotoBox(double scale, double offsetX, double offsetY)
    {
        var display = CameraView.Fit(cameraFrame, cameraAngle, CanvasNext.ViewportSize);
        if (display.IsEmpty)
        {
            return Rect.Empty;
        }

        return new Rect(
            (display.X + offsetX) / scale,
            (display.Y + offsetY) / scale,
            display.Width / scale,
            display.Height / scale);
    }
#endif
}
