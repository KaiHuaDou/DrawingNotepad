namespace LightBoard;

/// <summary>
/// 一次导出的参数；Path 对 PNG 与 SVG 是目标目录，对 PDF 是目标文件
/// </summary>
internal readonly record struct ExportOptions(ExportFormat Format, int Scale, string Path);
