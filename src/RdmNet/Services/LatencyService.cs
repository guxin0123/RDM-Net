using System.Diagnostics;
using System.Net.Sockets;
using RdmNet.Models;

namespace RdmNet.Services;

public enum LatencyState
{
    Unknown,

    /// <summary>检测进行中。</summary>
    Pending,

    Online,

    /// <summary>超时或连接失败。</summary>
    Offline,
}

public readonly record struct LatencyResult(LatencyState State, int Milliseconds, string? Detail);

/// <summary>通过 TCP 连接 RDP 端口测量延迟。</summary>
public static class LatencyService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private const int MaxConcurrency = 12;

    public static async Task<LatencyResult> ProbeAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new LatencyResult(LatencyState.Offline, 0, "服务器地址为空");
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            stopwatch.Stop();
            return new LatencyResult(LatencyState.Online, (int)stopwatch.ElapsedMilliseconds, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new LatencyResult(LatencyState.Offline, 0, "超时");
        }
        catch (SocketException ex)
        {
            return new LatencyResult(LatencyState.Offline, 0, Describe(ex));
        }
        catch (Exception ex)
        {
            return new LatencyResult(LatencyState.Offline, 0, ex.Message);
        }
    }

    /// <summary>
    /// 并发检测全部服务器，每完成一台就通过 <paramref name="report"/> 回报一次结果。
    /// </summary>
    public static async Task ScanAsync(
        IReadOnlyList<ConnectionServer> servers,
        int generation,
        Action<int, ConnectionServer, LatencyResult> report,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(MaxConcurrency);
        Task[] tasks = servers.Select(async server =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                LatencyResult result = await ProbeAsync(server.Host, server.Port, cancellationToken).ConfigureAwait(false);
                report(generation, server, result);
            }
            catch (OperationCanceledException)
            {
                // 本轮检测被取消（刷新或退出）。
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static string Describe(SocketException exception)
    {
        return exception.SocketErrorCode switch
        {
            SocketError.HostNotFound or SocketError.NoData => "域名解析失败",
            SocketError.ConnectionRefused => "连接被拒绝",
            SocketError.TimedOut => "超时",
            SocketError.NetworkUnreachable or SocketError.HostUnreachable => "网络不可达",
            _ => exception.Message,
        };
    }
}
