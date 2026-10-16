using System.IO;
using System.Text.Json;
using RdmNet.Utils;

namespace RdmNet.Services;

/// <summary>本程序写入过的一条系统凭据。</summary>
public sealed class PendingCredential
{
    public string Target { get; set; } = string.Empty;

    public int ProcessId { get; set; }

    public DateTime ProcessStartUtc { get; set; }

    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// pending_credentials.json 的读写：记录本程序写入过的 TERMSRV 目标，
/// 程序异常退出后下次启动据此清理，不会误删用户手动添加的凭据。
/// </summary>
public sealed class PendingCredentialStore
{
    private readonly object _gate = new();

    public IReadOnlyList<PendingCredential> Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(AppPaths.PendingCredentialsFile))
                {
                    return Array.Empty<PendingCredential>();
                }

                string json = File.ReadAllText(AppPaths.PendingCredentialsFile);
                return JsonSerializer.Deserialize<List<PendingCredential>>(json, StorageService.SerializerOptions)
                    ?? new List<PendingCredential>();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                return Array.Empty<PendingCredential>();
            }
        }
    }

    public void Add(PendingCredential entry)
    {
        lock (_gate)
        {
            List<PendingCredential> entries = LoadCore();
            entries.RemoveAll(e => string.Equals(e.Target, entry.Target, StringComparison.OrdinalIgnoreCase));
            entries.Add(entry);
            Write(entries);
        }
    }

    public void Remove(string target)
    {
        lock (_gate)
        {
            List<PendingCredential> entries = LoadCore();
            entries.RemoveAll(e => string.Equals(e.Target, target, StringComparison.OrdinalIgnoreCase));
            Write(entries);
        }
    }

    public void Replace(IEnumerable<PendingCredential> entries)
    {
        lock (_gate)
        {
            Write(entries.ToList());
        }
    }

    private static List<PendingCredential> LoadCore()
    {
        try
        {
            if (!File.Exists(AppPaths.PendingCredentialsFile))
            {
                return new List<PendingCredential>();
            }

            string json = File.ReadAllText(AppPaths.PendingCredentialsFile);
            return JsonSerializer.Deserialize<List<PendingCredential>>(json, StorageService.SerializerOptions)
                ?? new List<PendingCredential>();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new List<PendingCredential>();
        }
    }

    private static void Write(List<PendingCredential> entries)
    {
        StorageService.WriteAtomic(AppPaths.PendingCredentialsFile, JsonSerializer.Serialize(entries, StorageService.SerializerOptions));
    }
}
