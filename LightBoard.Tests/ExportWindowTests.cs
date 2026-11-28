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
                LastExportPath = @"D:\out",
            });
            try
            {
                Assert.True(window.SvgRadio.IsChecked);
                Assert.True(window.Scale50Radio.IsChecked);
                Assert.True(window.TimeRadio.IsChecked);
                Assert.Equal(@"D:\out", window.PathBox.Text);
                Assert.Matches(@"^\d{14}$", window.NameBox.Text);
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
                Assert.True(Path.IsPathRooted(window.PathBox.Text));
                Assert.Matches(@"^\d{14}$", window.NameBox.Text);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void NameMode_SwitchToCustom_RestoresInput( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings( ));
            try
            {
                window.CustomRadio.IsChecked = true;
                window.NameBox.Text = "会议记录";

                window.TimeRadio.IsChecked = true;
                Assert.Matches(@"^\d{14}$", window.NameBox.Text);

                window.CustomRadio.IsChecked = true;
                Assert.Equal("会议记录", window.NameBox.Text);
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
    public void TryBuildOptions_PdfWithName_AppendsPdf( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Pdf,
                LastExportPath = @"D:\out",
            });
            try
            {
                window.CustomRadio.IsChecked = true;
                window.NameBox.Text = "会议记录";

                Assert.True(window.TryBuildOptions(out var options, out _));
                Assert.Equal(@"D:\out\会议记录.pdf", options.Path);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_PdfNameWithExtension_StripsPdf( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Pdf,
                LastExportPath = @"D:\out",
            });
            try
            {
                window.CustomRadio.IsChecked = true;
                window.NameBox.Text = "会议记录.pdf";

                Assert.True(window.TryBuildOptions(out var options, out _));
                Assert.Equal(@"D:\out\会议记录.pdf", options.Path);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_PngWithName_UsesNameFolder( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings
            {
                LastExportFormat = ExportFormat.Png,
                LastExportPath = @"D:\out",
            });
            try
            {
                window.CustomRadio.IsChecked = true;
                window.NameBox.Text = "会议记录";

                Assert.True(window.TryBuildOptions(out var options, out _));
                Assert.Equal(@"D:\out\会议记录", options.Path);
            }
            finally
            {
                window.Close( );
            }
        });
    }

    [Fact]
    public void TryBuildOptions_EmptyName_ReturnsFalse( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings( ));
            try
            {
                window.CustomRadio.IsChecked = true;

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
    public void TryBuildOptions_InvalidName_ReturnsFalse( )
    {
        UiThread.Run(( ) =>
        {
            var window = new ExportWindow(new LightBoardSettings( ));
            try
            {
                window.CustomRadio.IsChecked = true;
                window.NameBox.Text = "a:b";

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
