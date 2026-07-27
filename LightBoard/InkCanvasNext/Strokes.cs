using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;
public partial class InkCanvasNext
{
    public void CopySelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        var copy = SelectedStrokes.Clone( );
        var stream = new MemoryStream( );
        copy.Save(stream);
        var data = new DataObject(StrokeCollection.InkSerializedFormat, stream);
        Clipboard.SetDataObject(data, true);
    }

    public void CutSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        CopySelected( );
        DeleteSelected( );
    }

    public void DeleteSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        Canvas.Strokes.Remove(SelectedStrokes);
    }

    public void Paste( )
    {
        if (!Clipboard.ContainsData(StrokeCollection.InkSerializedFormat))
        {
            return;
        }

        var data = Clipboard.GetData(StrokeCollection.InkSerializedFormat);
        if (data is not MemoryStream ms)
        {
            return;
        }

        ms.Position = 0;
        var strokes = new StrokeCollection(ms);

        var centerX = CanvasScroll.HorizontalOffset + CanvasScroll.ViewportWidth / 2;
        var centerY = CanvasScroll.VerticalOffset + CanvasScroll.ViewportHeight / 2;

        var bounds = strokes.GetBounds( );
        if (!bounds.IsEmpty)
        {
            var offsetX = centerX - (bounds.Left + bounds.Width / 2);
            var offsetY = centerY - (bounds.Top + bounds.Height / 2);
            var matrix = new Matrix(1, 0, 0, 1, offsetX, offsetY);
            foreach (var stroke in strokes)
            {
                stroke.Transform(matrix, false);
            }
        }

        Canvas.Strokes.Add(strokes);
    }

    public void CloneSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        var clone = SelectedStrokes.Clone( );

        var matrix = new Matrix(1, 0, 0, 1, 100, 100);

        foreach (var stroke in clone)
        {
            stroke.Transform(matrix, false);
        }

        Canvas.Strokes.Add(clone);
    }
}