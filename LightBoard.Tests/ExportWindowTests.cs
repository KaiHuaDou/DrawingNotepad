using System;
using System.IO;

using LightBoard.Tests.Toolbar;

namespace LightBoard.Tests;

public class ExportWindowTests
{
    [Fact]
    public void Window_Default_ReadsSettings( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Svg,
                LastExportScale = 50,
                LastExportPath = @"D:\out\a.svg",
            });
            try
            {
                Assert.True(window.SvgRadio.IsChecked);
                Assert.True(window.Scale50Radio.IsChecked);
                Assert.Equal(@"D:\out\a.svg", window.PathBox.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void Window_WithoutPath_FillsDefault( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings( ));
            try
            {
                Assert.Matches(@"\d{8}-\d{6}$", window.PathBox.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void FormatChanged_ToPdf_AppendsExtension( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Png,
                LastExportPath = @"D:\out\a",
            });
            try
            {
                window.PdfRadio.IsChecked = true;

                Assert.Equal(@"D:\out\a.pdf", window.PathBox.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void FormatChanged_ToSvg_RemovesExtension( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Pdf,
                LastExportPath = @"D:\out\a.pdf",
            });
            try
            {
                window.SvgRadio.IsChecked = true;

                Assert.Equal(@"D:\out\a", window.PathBox.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void FormatChanged_ToPng_KeepsDirectory( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Svg,
                LastExportPath = @"D:\out\a",
            });
            try
            {
                window.PngRadio.IsChecked = true;

                Assert.Equal(@"D:\out\a", window.PathBox.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_WithRelativePath_ReturnsAbsolute( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Png,
                LastExportPath = "a",
            });
            try
            {
                Assert.True(window.TryBuildOptions(out var options, out var error));
                Assert.True(Path.IsPathRooted(options.Path));
                Assert.Equal("", error);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_WithEmptyPath_ReturnsFalse( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings( ));
            try
            {
                window.PathBox.Text = "";

                Assert.False(window.TryBuildOptions(out _, out var error));
                Assert.NotEmpty(error);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_PdfWithoutExtension_AppendsPdf( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Pdf,
                LastExportPath = @"D:\out\a",
            });
            try
            {
                Assert.True(window.TryBuildOptions(out var options, out _));
                Assert.EndsWith(".pdf", options.Path);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_PngPathIsFile_ReturnsFalse( )
    {
        UiThread.Run(( ) =>
        {
            var file = Path.Join(Path.GetTempPath( ), $"lb-export-{Guid.NewGuid( ):N}");
            File.WriteAllText(file, "");
            try
            {
                var window = new ExportWindow(new LightBoardSettings
                {
                    LastExportFormat = ExportFormat.Png,
                    LastExportPath = file,
                });
                try
                {
                    Assert.False(window.TryBuildOptions(out _, out var error));
                    Assert.NotEmpty(error);
                }
                finally
                {
                    window.Close( );
                }
            }
            finally
            {
                File.Delete(file);
            }
        });
    }
}
