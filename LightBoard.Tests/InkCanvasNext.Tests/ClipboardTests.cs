using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Ink;

namespace InkCanvasNext.Tests;

/// <summary>剪贴板的边界路径。会临时改写系统剪贴板，结束后恢复原内容。</summary>
public class ClipboardTests
{
    [Fact]
    public void CopySelected_NoSelection_ReturnsFalse( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            Assert.False(host.Canvas.CopySelected( ));
        });
    }

    [Fact]
    public void CutSelected_NoSelection_ReturnsFalse( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            Assert.False(host.Canvas.CutSelected( ));
        });
    }

    [Fact]
    public void StampPasteAt_EmptyInkData_ReturnsFalse( )
    {
        StaTest.Run(( ) =>
        {
            using var host = new TouchHost( );

            IDataObject? previous;
            try
            {
                previous = Clipboard.GetDataObject( );
            }
            catch (ExternalException e)
            {
                Assert.Skip($"剪贴板不可用：{e.Message}");
                return;
            }

            try
            {
                var empty = new StrokeCollection( );
                using var stream = new MemoryStream( );
                empty.Save(stream);
                Clipboard.SetDataObject(new DataObject(StrokeCollection.InkSerializedFormat, stream), true);

                Assert.False(host.Canvas.StampPasteAt(new Point(100, 100)));
            }
            finally
            {
                if (previous is null)
                {
                    Clipboard.Clear( );
                }
                else
                {
                    Clipboard.SetDataObject(previous, true);
                }
            }
        });
    }
}
