using System.Diagnostics;
using RdmNet.Models;

namespace RdmNet.Services;

public readonly record struct CmdkeyResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>通过 cmdkey 管理系统凭据，实现 mstsc 免密登录。</summary>
public sealed class CredentialService
{
    /// <summary>
    /// 端口为默认端口时目标名为 TERMSRV/&lt;地址&gt;，否则附加端口。
    /// 删除时的目标名必须与写入时完全一致。
    /// </summary>
    public static string BuildTarget(string host, int port)
    {
        return port == ConnectionServer.DefaultPort
            ? $"TERMSRV/{host}"
            : $"TERMSRV/{host}:{port}";
    }

    public async Task<bool> AddAsync(string target, string username, string password, CancellationToken cancellationToken = default)
    {
        CmdkeyResult result = await RunAsync(new[] { "/generic:" + target, "/user:" + username, "/pass:" + password }, cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    /// <summary>该目标名是否存在凭据。</summary>
    public async Task<bool> ExistsAsync(string target, CancellationToken cancellationToken = default)
    {
        CmdkeyResult result = await RunAsync(new[] { "/list:" + target }, cancellationToken).ConfigureAwait(false);
        return result.StandardOutput.Contains(target, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> DeleteAsync(string target, CancellationToken cancellationToken = default)
    {
        CmdkeyResult result = await RunAsync(new[] { "/delete:" + target }, cancellationToken).ConfigureAwait(false);
        return result.Succeeded;
    }

    private static async Task<CmdkeyResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("cmdkey.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // 用 ArgumentList 逐个传参，避免密码中的空格、引号、& 等字符需要自行转义。
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 cmdkey.exe。");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new CmdkeyResult(process.ExitCode, await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
    }
}
