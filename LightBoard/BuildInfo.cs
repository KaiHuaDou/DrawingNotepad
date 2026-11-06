using System;
using System.Reflection;
using System.Runtime.InteropServices;

using ProcessArchitecture = System.Runtime.InteropServices.Architecture;

namespace LightBoard;

// 版本与构建变体由 csproj 与发布配置在编译期写入程序集
internal static class BuildInfo
{
    private const int ShortHashLength = 7;

    private static readonly AssemblyMetadataAttribute[] Metadata =
        [.. typeof(BuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>( )];

    private static readonly string Informational =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>( )?.InformationalVersion ?? string.Empty;

    public static string Version { get; } = BeforeRevision( );

    public static string Commit { get; } = AfterRevision( );

    public static bool WithOpenCv { get; } = ReadFlag("WithOpenCv");

    public static bool SelfContained { get; } = ReadFlag("SelfContained");

    // 发布产物按 RID 生成，运行进程的架构即产物架构
    public static string Architecture { get; } = RuntimeInformation.ProcessArchitecture switch
    {
        ProcessArchitecture.X64 => "x64",
        ProcessArchitecture.Arm64 => "arm64",
        var architecture => architecture.ToString( ),
    };

    private static string BeforeRevision( )
    {
        var plus = Informational.IndexOf('+');
        var version = plus < 0 ? Informational : Informational[..plus];
        return version.Length > 0 ? version : typeof(BuildInfo).Assembly.GetName( ).Version?.ToString(3) ?? "0.0.0";
    }

    // 源码链接把提交号接在版本后（1.3.0+<哈希>），源码目录没有仓库时为空
    private static string AfterRevision( )
    {
        var plus = Informational.IndexOf('+');
        if (plus < 0)
        {
            return "未知";
        }

        var hash = Informational[(plus + 1)..];
        return hash[..Math.Min(hash.Length, ShortHashLength)];
    }

    private static bool ReadFlag(string name)
    {
        foreach (var metadata in Metadata)
        {
            if (metadata.Key == name)
            {
                return bool.TryParse(metadata.Value, out var enabled) && enabled;
            }
        }

        return false;
    }
}
