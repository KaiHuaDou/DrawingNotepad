using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

// 导出窗口：收集格式、缩放与目标位置
public partial class ExportWindow : Window
{
    private readonly string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private ExportFormat format;
    private int scale;

    internal ExportOptions Options { get; private set; }

    public ExportWindow( ) : this(SettingsStore.Current)
    {
    }

    internal ExportWindow(LightBoardSettings settings)
    {
        InitializeComponent( );

        PngRadio.Checked += FormatChanged;
        PdfRadio.Checked += FormatChanged;
        SvgRadio.Checked += FormatChanged;
        Scale25Radio.Checked += ScaleChanged;
        Scale50Radio.Checked += ScaleChanged;
        Scale100Radio.Checked += ScaleChanged;

        format = settings.EffectiveExportFormat;
        scale = settings.EffectiveExportScale;

        SetChecked( );
        PathBox.Text = settings.EffectiveExportPath is { Length: > 0 } path ? path : DefaultPath(format);
        PageCountText.Text = $"共 {BoardState.Pages.Count} 页";
        UpdateHint( );
    }

    private void FormatChanged(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Tag: string tag }
            || !Enum.TryParse<ExportFormat>(tag, out var value)
            || value == format)
        {
            return;
        }

        var previous = format;
        format = value;
        PathBox.Text = PathBox.Text.Length == 0
            ? DefaultPath(format)
            : ConvertPath(PathBox.Text, previous, format);
        UpdateHint( );
    }

    private void ScaleChanged(object o, RoutedEventArgs e)
    {
        if (o is RadioButton { Tag: string tag } && int.TryParse(tag, out var value))
        {
            scale = value;
        }
    }

    private void BrowseClick(object o, RoutedEventArgs e)
    {
        if (format == ExportFormat.Pdf)
        {
            var dialog = new VistaSaveFileDialog( )
            {
                Filter = "PDF 文档 (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = Path.GetFileName(PathBox.Text),
            };
            if (dialog.ShowDialog( ) == true)
            {
                PathBox.Text = dialog.FileName;
            }

            return;
        }

        var folder = new VistaFolderBrowserDialog( )
        {
            SelectedPath = PathBox.Text,
            ShowNewFolderButton = true,
        };
        if (folder.ShowDialog( ) == true)
        {
            PathBox.Text = Path.Join(folder.SelectedPath, timestamp);
        }
    }

    private void ExportClick(object o, RoutedEventArgs e)
    {
        if (!TryBuildOptions(out var options, out var error))
        {
            App.ShowInfo(error);
            return;
        }

        if (!EnsureDirectory(options, out error))
        {
            App.ShowInfo(error);
            return;
        }

        if (NeedsOverwriteConfirm(options) && !ConfirmOverwrite( ))
        {
            return;
        }

        SavePreferences(options);
        Options = options;
        DialogResult = true;
    }

    internal bool TryBuildOptions(out ExportOptions options, out string error)
    {
        var text = PathBox.Text.Trim( );
        if (text.Length == 0)
        {
            options = default;
            error = "请填写导出位置";
            return false;
        }

        string path;
        try
        {
            path = Path.GetFullPath(text);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            options = default;
            error = "导出位置无效";
            return false;
        }

        if (format == ExportFormat.Pdf)
        {
            if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                path = $"{path}.pdf";
            }

            if (Directory.Exists(path))
            {
                options = default;
                error = "导出位置是目录，请指定文件名";
                return false;
            }
        }
        else if (File.Exists(path))
        {
            options = default;
            error = "导出位置是文件，请指定目录";
            return false;
        }

        options = new(format, scale, path);
        error = string.Empty;
        return true;
    }

    private void SetChecked( )
    {
        var formatRadio = format switch
        {
            ExportFormat.Pdf => PdfRadio,
            ExportFormat.Svg => SvgRadio,
            _ => PngRadio,
        };
        formatRadio.IsChecked = true;

        var scaleRadio = scale switch
        {
            25 => Scale25Radio,
            50 => Scale50Radio,
            _ => Scale100Radio,
        };
        scaleRadio.IsChecked = true;
    }

    private void UpdateHint( )
    {
        FormatHintText.Text = format switch
        {
            ExportFormat.Png => "每页导出为一个 PNG 文件，按页码命名",
            ExportFormat.Pdf => "整板导出为单个 PDF 文件",
            _ => "每页导出为一个 SVG 文件，按页码命名",
        };
    }

    private string DefaultPath(ExportFormat value)
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrEmpty(directory))
        {
            directory = App.AppPath;
        }

        return value == ExportFormat.Pdf
            ? Path.Join(directory, $"{timestamp}.pdf")
            : Path.Join(directory, timestamp);
    }

    private static string ConvertPath(string path, ExportFormat from, ExportFormat to)
    {
        var wasFile = from == ExportFormat.Pdf;
        var isFile = to == ExportFormat.Pdf;
        if (wasFile == isFile)
        {
            return path;
        }

        if (isFile)
        {
            return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? path : $"{path}.pdf";
        }

        return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
    }

    private static bool EnsureDirectory(ExportOptions options, out string error)
    {
        var directory = options.Format == ExportFormat.Pdf
            ? Path.GetDirectoryName(options.Path)
            : options.Path;
        if (string.IsNullOrEmpty(directory))
        {
            error = "导出位置无效";
            return false;
        }

        try
        {
            Directory.CreateDirectory(directory);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            error = "无法创建目标文件夹";
            return false;
        }
    }

    private static bool NeedsOverwriteConfirm(ExportOptions options)
    {
        if (options.Format == ExportFormat.Pdf)
        {
            return File.Exists(options.Path);
        }

        try
        {
            return Directory.EnumerateFileSystemEntries(options.Path).Any( );
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ConfirmOverwrite( )
    {
        using TaskDialog dialog = new( )
        {
            WindowTitle = "轻白板",
            MainInstruction = "目标已有文件，是否覆盖？",
            MainIcon = TaskDialogIcon.Warning,
        };
        var overwrite = new TaskDialogButton("覆盖");
        dialog.Buttons.Add(overwrite);
        dialog.Buttons.Add(new TaskDialogButton(ButtonType.Cancel));
        return dialog.ShowDialog( ) == overwrite;
    }

    private static void SavePreferences(ExportOptions options)
    {
        try
        {
            SettingsStore.Save(SettingsStore.Current with
            {
                LastExportFormat = options.Format,
                LastExportScale = options.Scale,
                LastExportPath = options.Path,
            });
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }
}
