using System.IO;

namespace RdmNet.Utils;

/// <summary>程序数据文件所在目录。</summary>
public static class AppPaths
{
    /// <summary>
    /// 单文件发布下 AppContext.BaseDirectory 可能指向 %TEMP% 解压目录，
    /// 因此以 exe 自身路径为准。
    /// </summary>
    public static string BaseDirectory { get; } = ResolveBaseDirectory();

    public static string ConnectionsFile { get; } = Path.Combine(BaseDirectory, "connections.json");

    public static string PendingCredentialsFile { get; } = Path.Combine(BaseDirectory, "pending_credentials.json");

    private static string ResolveBaseDirectory()
    {
        string? directory = Path.GetDirectoryName(Environment.ProcessPath);
        return string.IsNullOrEmpty(directory) ? AppContext.BaseDirectory : directory;
    }
}
