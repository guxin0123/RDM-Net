namespace RdmNet.Models;

/// <summary>密码字段的编码格式。</summary>
public static class PasswordFormats
{
    /// <summary>Windows DPAPI（CryptProtectData）密文的 base64。</summary>
    public const string Dpapi = "dpapi";

    /// <summary>导出时用导出密码经 AES-GCM 加密。</summary>
    public const string AesGcm = "aes-gcm";
}

/// <summary>一条已保存的 RDP 连接（界面中称为“服务器”）。</summary>
public sealed class ConnectionServer
{
    public const int DefaultPort = 3389;

    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = DefaultPort;

    public string Username { get; set; } = string.Empty;

    /// <summary>DPAPI 密文的 base64；密码为空时为 null。</summary>
    public string? Password { get; set; }

    public string? PasswordFormat { get; set; }

    /// <summary>仅导出文件中出现：用导出密码加密后的密码。</summary>
    public EncryptedPassword? PasswordEncrypted { get; set; }
}

/// <summary>导出文件中经导出密码加密的密码。</summary>
public sealed class EncryptedPassword
{
    public string Nonce { get; set; } = string.Empty;

    public string Ciphertext { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;
}

/// <summary>分组（仅支持单层，不支持嵌套）。</summary>
public sealed class ConnectionGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    public string Name { get; set; } = string.Empty;

    public List<ConnectionServer> Servers { get; set; } = new();
}

/// <summary>connections.json 的根结构。</summary>
public sealed class ConnectionsFile
{
    public int Version { get; set; } = 1;

    public List<ConnectionGroup> Groups { get; set; } = new();
}

/// <summary>导出文件中派生密钥用的 KDF 参数。</summary>
public sealed class ExportKdf
{
    public string Name { get; set; } = "PBKDF2-SHA256";

    public int Iterations { get; set; }

    public string Salt { get; set; } = string.Empty;
}

/// <summary>导入 / 导出的 JSON 根结构。</summary>
public sealed class ExportDocument
{
    public int Version { get; set; } = 1;

    public bool Encrypted { get; set; }

    public ExportKdf? Kdf { get; set; }

    public List<ConnectionGroup> Groups { get; set; } = new();
}

/// <summary>分组相关的公共操作。</summary>
public static class ConnectionData
{
    public const string DefaultGroupName = "默认";

    /// <summary>确保默认分组存在，并返回它。</summary>
    public static ConnectionGroup EnsureDefaultGroup(ConnectionsFile data)
    {
        ConnectionGroup? existing = FindGroupByName(data, DefaultGroupName);
        if (existing is not null)
        {
            return existing;
        }

        var group = new ConnectionGroup { Name = DefaultGroupName };
        data.Groups.Add(group);
        return group;
    }

    public static ConnectionGroup? FindGroupByName(ConnectionsFile data, string name)
    {
        return data.Groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool GroupNameExists(ConnectionsFile data, string name, ConnectionGroup? except = null)
    {
        return data.Groups.Any(g => g != except && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>删除分组，组内服务器移入默认分组。</summary>
    public static void RemoveGroup(ConnectionsFile data, ConnectionGroup group)
    {
        ConnectionGroup fallback = EnsureDefaultGroup(data);
        foreach (ConnectionServer server in group.Servers)
        {
            fallback.Servers.Add(server);
        }

        if (!ReferenceEquals(fallback, group))
        {
            data.Groups.Remove(group);
        }
    }

    public static IEnumerable<ConnectionServer> AllServers(ConnectionsFile data)
    {
        return data.Groups.SelectMany(g => g.Servers);
    }
}
