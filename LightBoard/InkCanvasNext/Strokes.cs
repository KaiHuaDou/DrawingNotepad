using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace InkCanvasNext;

public partial class InkCanvasNext
{
    /// <summary>
    /// 获取当前选中的笔画数量。
    /// </summary>
    public int SelectedCount => selection.SelectedStrokes.Count;

    /// <summary>
    /// 获取当前选中的笔画快照；笔画实例与画布共享。
    /// </summary>
    public IReadOnlyList<Stroke> SelectedStrokes => Array.AsReadOnly(selection.SelectedStrokes.ToArray( ));

    /// <summary>
    /// 获取是否存在选中笔画。
    /// </summary>
    public bool HasSelection => selection.HasSelection;

    /// <summary>
    /// 复制选中笔画到剪贴板，返回是否写入成功。
    /// </summary>
    public bool CopySelected( )
    {
        if (!HasSelection)
        {
            return false;
        }

        try
        {
            var copy = new StrokeCollection([.. selection.SelectedStrokes.Select(s => s.Clone( ))]);
            using var stream = new MemoryStream( );
            copy.Save(stream);
            var data = new DataObject(StrokeCollection.InkSerializedFormat, stream);
            Clipboard.SetDataObject(data, true);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    /// <summary>
    /// 剪切选中笔画到剪贴板并从画布删除，返回是否写入成功。
    /// </summary>
    public bool CutSelected( )
    {
        if (!HasSelection || !CopySelected( ))
        {
            return false;
        }

        DeleteSelected( );
        return true;
    }

    /// <summary>
    /// 删除选中的笔画。
    /// </summary>
    public void DeleteSelected( )
    {
        if (!HasSelection)
        {
            return;
        }

        InnerCanvas.Strokes.Remove([with([.. selection.SelectedStrokes])]);
        selection.Clear( );
    }

    /// <summary>
    /// 在指定位置克隆一份选区笔画。
    /// </summary>
    public void StampCloneAt(Point point)
    {
        if (!HasSelection)
        {
            return;
        }

        // 深拷贝必须保留：落章要对副本平移并加入画布，浅引用会拖动原选中笔画
        var clone = new StrokeCollection([.. selection.SelectedStrokes.Select(s => s.Clone( ))]);
        CenterAt(clone, point);
        InnerCanvas.Strokes.Add(clone);
        EnsureStrokesFit( );
    }

    /// <summary>
    /// 在指定位置粘贴剪贴板中的墨迹笔画，返回是否粘贴成功。
    /// </summary>
    public bool StampPasteAt(Point point)
    {
        try
        {
            if (!Clipboard.ContainsData(StrokeCollection.InkSerializedFormat))
            {
                return false;
            }

            var data = Clipboard.GetData(StrokeCollection.InkSerializedFormat);
            if (data is not MemoryStream ms)
            {
                return false;
            }

            ms.Position = 0;
            var strokes = new StrokeCollection(ms);
            if (strokes.Count == 0)
            {
                return false;
            }

            CenterAt(strokes, point);
            InnerCanvas.Strokes.Add(strokes);
            EnsureStrokesFit( );
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 把笔画落入当前视口：包围盒不完全在可见内容区域内时平移到视口中心，否则按原坐标保留。
    /// </summary>
    public void EnsureStrokesVisible(StrokeCollection strokes)
    {
        // 布局完成前视口尺寸为 0，无法计算可见区域，此时不落位
        if (CanvasScroll.ViewportWidth <= 0 || CanvasScroll.ViewportHeight <= 0)
        {
            return;
        }

        var bounds = strokes.GetBounds( );
        if (bounds.IsEmpty)
        {
            return;
        }

        var view = CurrentView;
        var visible = new Rect(
            view.OffsetX / view.Scale,
            view.OffsetY / view.Scale,
            CanvasScroll.ViewportWidth / view.Scale,
            CanvasScroll.ViewportHeight / view.Scale);
        if (visible.Contains(bounds))
        {
            return;
        }

        CenterAt(strokes, new Point(visible.X + visible.Width / 2, visible.Y + visible.Height / 2));
    }

    private static void CenterAt(StrokeCollection strokes, Point point)
    {
        var bounds = strokes.GetBounds( );
        if (bounds.IsEmpty)
        {
            return;
        }

        var offsetX = point.X - (bounds.Left + bounds.Width / 2);
        var offsetY = point.Y - (bounds.Top + bounds.Height / 2);
        var matrix = new Matrix(1, 0, 0, 1, offsetX, offsetY);
        foreach (var stroke in strokes)
        {
            stroke.Transform(matrix, false);
        }
    }
}
