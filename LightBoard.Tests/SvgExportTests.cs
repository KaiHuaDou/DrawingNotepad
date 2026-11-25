using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

public class SvgExportTests
{
    [Fact]
    public void ExportAll_MultiplePages_WritesNumberedFilesAndProgress( )
    {
        UiThread.Run(( ) =>
        {
            BoardState.Pages.Clear( );
            BoardState.InitializePages( );
            BoardState.NewPage( );
            BoardState.NewPage( );

            var directory = Path.Join(Path.GetTempPath( ), $"lb-svg-test-{Guid.NewGuid( ):N}");
            var progress = new CollectProgress( );
            try
            {
                SvgExport.ExportAll(directory, 100, progress);

                var files = Directory.GetFiles(directory, "*.svg").Order( ).ToArray( );
                Assert.Equal(3, files.Length);
                Assert.EndsWith("1.svg", files[0]);
                Assert.EndsWith("2.svg", files[1]);
                Assert.EndsWith("3.svg", files[2]);
                foreach (var file in files)
                {
                    XDocument.Parse(File.ReadAllText(file));
                }

                Assert.Equal(4, progress.Items.Count);
                Assert.Equal(0, progress.Items[0].Done);
                Assert.Equal("正在导出 SVG", progress.Items[0].Tip);
                Assert.Equal(3, progress.Items[^1].Done);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }

                BoardState.Pages.Clear( );
                BoardState.InitializePages( );
            }
        });
    }
}

internal sealed class CollectProgress : IProgress<ExportProgress>
{
    public List<ExportProgress> Items { get; } = [ ];

    public void Report(ExportProgress value)
    {
        Items.Add(value);
    }
}
