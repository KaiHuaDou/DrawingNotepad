using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Ookii.Dialogs.Wpf;

namespace LightBoard;

// 导出窗口：收集格式、缩放、路径与名称
public partial class ExportWindow : Window
{
    private readonly string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
    private ExportFormat format;
    private int scale;
    private bool timeName = true;
    private string customName = string.Empty;

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
        TimeRadio.Checked += NameModeChanged;
        CustomRadio.Checked += NameModeChanged;

        format = settings.EffectiveExportFormat;
        scale = settings.EffectiveExportScale;

        SetChecked( );
        TimeRadio.IsChecked = true;
        PathBox.Text = settings.EffectiveExportPath is { Length: > 0 } path ? path : DefaultDirectory( );
        PageCountText.Text = $"共 {BoardState.Pages.Count} 页";
    }

    private void FormatChanged(object o, RoutedEventArgs e)
    {
        if (o is RadioButton { Tag: string tag } && Enum.TryParse<ExportFormat>(tag, out var value))
        {
            format = value;
        }
    }

    private void ScaleChanged(object o, RoutedEventArgs e)
    {
        if (o is RadioButton { Tag: string tag } && int.TryParse(tag, out var value))
        {
            scale = value;
        }
    }

    private void NameModeChanged(object o, RoutedEventArgs e)
    {
        if (o is not RadioButton { Tag: string tag })
        {
            return;
        }

        if (!timeName)
        {
            customName = NameBox.Text;
        }

        timeName = tag == "Time";
        NameBox.IsReadOnly = timeName;
        NameBox.Text = timeName ? timestamp : customName;
        if (!timeName)
        {
            NameBox.Focus( );
            NameBox.SelectAll( );
        }
    }

    private void BrowseClick(object o, RoutedEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog( )
        {
            SelectedPath = PathBox.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog( ) == true)
        {
            PathBox.Text = dialog.SelectedPath;
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
            error = "请填写导出路径";
            return false;
        }

        string directory;
        try
        {
            directory = Path.GetFullPath(text);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            options = default;
            error = "导出路径无效";
            return false;
        }

        if (File.Exists(directory))
        {
            options = default;
            error = "导出路径是文件，请填写目录";
            return false;
        }

        var name = TrimName(NameBox.Text, format);
        if (name.Length == 0)
        {
            options = default;
            error = "请填写名称";
            return false;
        }

        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars( )) >= 0)
        {
            options = default;
            error = "名称包含无效字符";
            return false;
        }

        options = new(format, scale, format == ExportFormat.Pdf
            ? Path.Join(directory, $"{name}.pdf")
            : Path.Join(directory, name));
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

    private static string DefaultDirectory( )
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return directory.Length == 0 ? App.AppPath : directory;
    }

    private static string TrimName(string text, ExportFormat value)
    {
        var name = text.Trim( );
        var extension = value switch
        {
            ExportFormat.Pdf => ".pdf",
            ExportFormat.Png => ".png",
            _ => ".svg",
        };
        return name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? name[..^extension.Length].TrimEnd( )
            : name;
    }

    private static bool EnsureDirectory(ExportOptions options, out string error)
    {
        var directory = options.Directory;
        if (directory.Length == 0)
        {
            error = "导出路径无效";
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
                LastExportPath = options.Directory,
            });
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }
}
