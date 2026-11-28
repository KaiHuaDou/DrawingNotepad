namespace LightBoard;

/// <summary>
/// 一次导出的参数；Path 对 PNG 与 SVG 是目标文件夹，对 PDF 是目标文件
/// </summary>
internal readonly record struct ExportOptions(ExportFormat Format, int Scale, string Path)
{
    /// <summary>
    /// 导出目录
    /// </summary>
    internal string Directory => Format == ExportFormat.Pdf ? System.IO.Path.GetDirectoryName(Path) ?? string.Empty : Path;
}
