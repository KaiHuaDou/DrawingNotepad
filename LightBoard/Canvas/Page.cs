using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Ink;

using InkCanvasNext;

namespace LightBoard;

public partial class Page : INotifyPropertyChanged
{
    public int Number
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
        }
    }
    public StrokeCollection Strokes { get; set; } = [];
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = CanvasViewport.InitialOffset.X;
    public double OffsetY { get; set; } = CanvasViewport.InitialOffset.Y;
    public HistorySnapshot? History { get; set; }
    internal DocumentBackground? Background { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    // 页视图不在此重置，由调用方与画布视图同步
    public void OpenStrokes(string fileName)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        Strokes = [with(stream)];
        History = null;
        InvalidatePreview( );
        BoardState.MarkChanged( );
    }

    /// <summary>
    /// 内容坐标整体平移后同步背景盒与页视图
    /// </summary>
    internal void RebaseContent(Vector shift, double scale)
    {
        if (Background is { } background)
        {
            Background = background with
            {
                Box = new Rect(
                    background.Box.X + shift.X,
                    background.Box.Y + shift.Y,
                    background.Box.Width,
                    background.Box.Height)
            };
        }

        OffsetX += shift.X * scale;
        OffsetY += shift.Y * scale;
    }
}
