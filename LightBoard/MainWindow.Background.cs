using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LightBoard;

internal enum BackgroundKind
{
    Grid,
    White,
    Black,
    Transparent,
    Video,
}

public partial class MainWindow
{
    /// <summary>
    /// 背景单选的唯一解码点：画布底色、窗口穿透与视频启停都按这里的结果分派
    /// </summary>
    private BackgroundKind CurrentBackground( )
    {
        return VideoBackgroundRadio.IsChecked == true ? BackgroundKind.Video
            : WhiteBackgroundRadio.IsChecked == true ? BackgroundKind.White
            : BlackBackgroundRadio.IsChecked == true ? BackgroundKind.Black
            : TransparentBackgroundRadio.IsChecked == true ? BackgroundKind.Transparent
            : BackgroundKind.Grid;
    }

    private void ApplyBackground( )
    {
        var kind = CurrentBackground( );

#if OPENCV
        if (kind == BackgroundKind.Video)
        {
            StartVideoStand( );
        }
        else if (videoPage is not null)
        {
            videoPage = null;
            StopVideoStand( );
        }
#endif

        RefreshVideoBackground( );

        var transparent = kind == BackgroundKind.Transparent;
        TimeText.Visibility = transparent || AllPageToggle.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        SwitchOutButton.Visibility = transparent ? Visibility.Collapsed : Visibility.Visible;
        LeftBorder.Background = transparent ? ContainerBrushSolid : ContainerBrush;
        CenterBorder.Background = transparent ? ContainerBrushSolid : ContainerBrush;
        RightBorder.Background = transparent ? ContainerBrushSolid : ContainerBrush;
        PassThroughBorder.Visibility = transparent ? Visibility.Visible : Visibility.Collapsed;
        Topmost = transparent;
    }

    // 视频页的画布底色仍是网格，视频画面由 SetDocumentPage 盖在其上
    private static Brush BackgroundBrush(BackgroundKind kind)
    {
        return kind switch
        {
            BackgroundKind.White => Brushes.White,
            BackgroundKind.Black => Brushes.Black,
            BackgroundKind.Transparent => Brushes.Transparent,
            _ => MajorGridBrush,
        };
    }

    private void BackgroundRadioClick(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { IsChecked: true })
        {
            return;
        }

#if OPENCV
        // 组互斥在 Click 之前已取消其余单选，最近勾选的全局背景只能在这里记录
        if (!ReferenceEquals(o, VideoBackgroundRadio))
        {
            globalBackgroundRadio = (RadioButton) o;
        }
#endif

        ApplyBackground( );
    }

    // 视频背景仅在含 OpenCV 的构建中存在；nocv 构建的画布底色只随背景单选走
#if !OPENCV
    private void RefreshVideoBackground( )
    {
        CanvasNext.Background = BackgroundBrush(CurrentBackground( ));
    }
#endif
}
