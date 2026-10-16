using System.Diagnostics;
using RdmNet.Models;

namespace RdmNet.Services;

public readonly record struct ConnectOutcome(bool Started, string? Message);

/// <summary>
/// mstsc 集成：写入凭据 → 启动远程桌面 → 窗口关闭后删除凭据。
/// </summary>
public sealed class RdpLauncher
{
    private const string MstscProcessName = "mstsc";
    private static readonly TimeSpan WindowStartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProcessPollInterval = TimeSpan.FromMilliseconds(500);

    private readonly CredentialService _credentials = new();
    private readonly PendingCredentialStore _pending = new();
    private readonly Dictionary<string, int> _referenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public async Task<ConnectOutcome> ConnectAsync(ConnectionServer server, string? plainPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(server.Host))
        {
            return new ConnectOutcome(false, "服务器地址为空，无法连接。");
        }

        string target = CredentialService.BuildTarget(server.Host, server.Port);
        bool credentialWritten = false;
        string? warning = null;

        if (!string.IsNullOrEmpty(plainPassword))
        {
            if (await _credentials.AddAsync(target, server.Username, plainPassword, cancellationToken).ConfigureAwait(false))
            {
                credentialWritten = true;
                AddReference(target);
            }
            else
            {
                warning = "写入系统凭据失败，需要在远程桌面窗口中手动输入密码。";
            }
        }

        int[] existingPids = GetMstscPids();
        var startInfo = new ProcessStartInfo("mstsc.exe") { UseShellExecute = false };
        startInfo.ArgumentList.Add($"/v:{server.Host}:{server.Port}");
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            if (credentialWritten)
            {
                await ReleaseAsync(target, cancellationToken).ConfigureAwait(false);
            }

            return new ConnectOutcome(false, $"无法启动 mstsc：{ex.Message}");
        }

        // Win10+ 的 mstsc.exe 启动后会立刻退出并派生同名子进程，
        // 因此用 PID 差集定位真正承载窗口的进程。
        int? windowPid = await WaitForNewProcessAsync(existingPids, WindowStartTimeout, cancellationToken).ConfigureAwait(false);
        if (windowPid is null)
        {
            if (credentialWritten)
            {
                await ReleaseAsync(target, cancellationToken).ConfigureAwait(false);
            }

            return new ConnectOutcome(false, "mstsc 未能在 30 秒内启动。");
        }

        if (credentialWritten)
        {
            _pending.Add(new PendingCredential
            {
                Target = target,
                ProcessId = windowPid.Value,
                ProcessStartUtc = GetProcessStartUtc(windowPid.Value),
            });
            _ = WatchProcessAsync(windowPid.Value, target);
        }

        return new ConnectOutcome(true, warning);
    }

    /// <summary>程序退出时清理本程序写入的全部凭据。</summary>
    public async Task ReleaseAllAsync()
    {
        string[] targets;
        lock (_gate)
        {
            targets = _referenceCounts.Keys.ToArray();
            _referenceCounts.Clear();
        }

        foreach (string target in targets)
        {
            await _credentials.DeleteAsync(target).ConfigureAwait(false);
        }

        _pending.Replace(Array.Empty<PendingCredential>());
    }

    /// <summary>启动时清理上次异常退出遗留的凭据；仍在使用的进程对应的条目予以保留。</summary>
    public async Task CleanupLeftoversAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingCredential> entries = _pending.Load();
        var remaining = new List<PendingCredential>();
        foreach (PendingCredential entry in entries)
        {
            if (IsProcessAlive(entry.ProcessId, entry.ProcessStartUtc))
            {
                remaining.Add(entry);
                continue;
            }

            await _credentials.DeleteAsync(entry.Target, cancellationToken).ConfigureAwait(false);
        }

        _pending.Replace(remaining);
    }

    private async Task WatchProcessAsync(int processId, string target)
    {
        try
        {
            while (IsProcessAlive(processId, GetProcessStartUtc(processId)))
            {
                await Task.Delay(ProcessPollInterval).ConfigureAwait(false);
            }

            await ReleaseAsync(target).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 监听线程的异常不应影响界面。
        }
    }

    private async Task ReleaseAsync(string target, CancellationToken cancellationToken = default)
    {
        if (!ReleaseReference(target))
        {
            return;
        }

        await _credentials.DeleteAsync(target, cancellationToken).ConfigureAwait(false);
        _pending.Remove(target);
    }

    private void AddReference(string target)
    {
        lock (_gate)
        {
            _referenceCounts.TryGetValue(target, out int count);
            _referenceCounts[target] = count + 1;
        }
    }

    /// <summary>引用计数归零时返回 true（此时才可以删除凭据）。</summary>
    private bool ReleaseReference(string target)
    {
        lock (_gate)
        {
            if (!_referenceCounts.TryGetValue(target, out int count))
            {
                return false;
            }

            if (count <= 1)
            {
                _referenceCounts.Remove(target);
                return true;
            }

            _referenceCounts[target] = count - 1;
            return false;
        }
    }

    private static async Task<int?> WaitForNewProcessAsync(int[] existingPids, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int[] currentPids = GetMstscPids();
            int[] newPids = currentPids.Where(id => !existingPids.Contains(id)).ToArray();
            if (newPids.Length > 0)
            {
                return newPids.Max();
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private static int[] GetMstscPids()
    {
        Process[] processes = Process.GetProcessesByName(MstscProcessName);
        try
        {
            return processes.Select(p => p.Id).ToArray();
        }
        finally
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsProcessAlive(int processId, DateTime expectedStartUtc)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return false;
            }

            if (expectedStartUtc != default)
            {
                DateTime actualStartUtc = process.StartTime.ToUniversalTime();
                if (Math.Abs((actualStartUtc - expectedStartUtc).TotalSeconds) > 2)
                {
                    // PID 已被复用。
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static DateTime GetProcessStartUtc(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return default;
        }
    }
}
