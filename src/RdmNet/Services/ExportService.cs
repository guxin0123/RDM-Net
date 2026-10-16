using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RdmNet.Models;

namespace RdmNet.Services;

public enum ConflictAction
{
    Overwrite,

    Skip,

    Cancel,
}

public delegate ConflictAction ConflictResolver(ConnectionGroup group, ConnectionServer existing, ConnectionServer incoming);

public readonly record struct ExportResult(int ServersSkipped);

public readonly record struct ImportResult(int ServersAdded, int ServersOverwritten, int ServersSkipped, bool Canceled);

/// <summary>
/// 连接列表的 JSON 导入导出。导出时密码默认不导出（DPAPI 密文无法被其他机器解密），
/// 可选使用导出密码以 AES-GCM 加密后写入。
/// </summary>
public sealed class ExportService
{
    private const int Iterations = 200_000;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    public ExportResult Export(ConnectionsFile data, string path, string? password)
    {
        int skipped = 0;
        var document = new ExportDocument { Version = 1, Encrypted = !string.IsNullOrEmpty(password) };
        byte[]? key = null;
        try
        {
            if (document.Encrypted)
            {
                byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
                key = Rfc2898DeriveBytes.Pbkdf2(password!, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
                document.Kdf = new ExportKdf
                {
                    Name = "PBKDF2-SHA256",
                    Iterations = Iterations,
                    Salt = Convert.ToBase64String(salt),
                };
            }

            foreach (ConnectionGroup group in data.Groups)
            {
                var exportGroup = new ConnectionGroup { Name = group.Name };
                foreach (ConnectionServer server in group.Servers)
                {
                    var exported = new ConnectionServer
                    {
                        Name = server.Name,
                        Host = server.Host,
                        Port = server.Port,
                        Username = server.Username,
                    };

                    if (key is not null && !string.IsNullOrEmpty(server.Password))
                    {
                        string? plain = TryUnprotect(server.Password);
                        if (string.IsNullOrEmpty(plain))
                        {
                            skipped++;
                        }
                        else
                        {
                            exported.PasswordEncrypted = Encrypt(plain, key);
                            exported.PasswordFormat = PasswordFormats.AesGcm;
                        }
                    }

                    exportGroup.Servers.Add(exported);
                }

                document.Groups.Add(exportGroup);
            }

            string json = JsonSerializer.Serialize(document, StorageService.SerializerOptions);
            StorageService.WriteAtomic(path, json);
            return new ExportResult(skipped);
        }
        finally
        {
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    public ImportResult Import(string path, string? password, ConnectionsFile target, ConflictResolver resolve)
    {
        ExportDocument document = ReadDocument(path);
        byte[]? key = null;
        try
        {
            if (document.Encrypted)
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException("导入文件中的密码已加密，需要输入导出时使用的密码。");
                }

                ExportKdf kdf = document.Kdf ?? throw new InvalidOperationException("导入文件缺少密钥派生参数。");
                byte[] salt = Convert.FromBase64String(kdf.Salt);
                key = Rfc2898DeriveBytes.Pbkdf2(password, salt, kdf.Iterations, HashAlgorithmName.SHA256, KeySize);
            }

            int added = 0;
            int overwritten = 0;
            int skipped = 0;
            foreach (ConnectionGroup importGroup in document.Groups)
            {
                string groupName = string.IsNullOrWhiteSpace(importGroup.Name) ? ConnectionData.DefaultGroupName : importGroup.Name;
                ConnectionGroup group = ConnectionData.FindGroupByName(target, groupName) ?? AddGroup(target, groupName);
                foreach (ConnectionServer importServer in importGroup.Servers)
                {
                    ConnectionServer server = ToStoredServer(importServer, key);
                    ConnectionServer? existing = group.Servers.FirstOrDefault(s => string.Equals(s.Name, server.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing is null)
                    {
                        group.Servers.Add(server);
                        added++;
                        continue;
                    }

                    switch (resolve(group, existing, server))
                    {
                        case ConflictAction.Overwrite:
                            existing.Host = server.Host;
                            existing.Port = server.Port;
                            existing.Username = server.Username;
                            existing.Password = server.Password;
                            existing.PasswordFormat = server.PasswordFormat;
                            overwritten++;
                            break;
                        case ConflictAction.Skip:
                            skipped++;
                            break;
                        default:
                            return new ImportResult(added, overwritten, skipped, Canceled: true);
                    }
                }
            }

            return new ImportResult(added, overwritten, skipped, Canceled: false);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("导出密码不正确，或文件已损坏。", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("导入文件中的密文不是合法的 base64 数据。", ex);
        }
        finally
        {
            if (key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }

    public static ExportDocument ReadDocument(string path)
    {
        string json = File.ReadAllText(path);
        ExportDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ExportDocument>(json, StorageService.SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"导入文件格式不正确：{ex.Message}", ex);
        }

        return document ?? throw new InvalidOperationException("导入文件内容为空。");
    }

    private static ConnectionGroup AddGroup(ConnectionsFile data, string name)
    {
        var group = new ConnectionGroup { Name = name };
        data.Groups.Add(group);
        return group;
    }

    private static ConnectionServer ToStoredServer(ConnectionServer source, byte[]? key)
    {
        var server = new ConnectionServer
        {
            Name = source.Name,
            Host = source.Host,
            Port = source.Port is > 0 and <= 65535 ? source.Port : ConnectionServer.DefaultPort,
            Username = source.Username,
        };

        if (key is not null && source.PasswordEncrypted is not null)
        {
            string plain = Decrypt(source.PasswordEncrypted, key);
            server.Password = DpapiService.Protect(plain);
            server.PasswordFormat = server.Password is null ? null : PasswordFormats.Dpapi;
        }
        else if (!string.IsNullOrEmpty(source.Password) && source.PasswordFormat == PasswordFormats.Dpapi)
        {
            // 同一台机器上的同一用户可直接复用原有 DPAPI 密文。
            server.Password = source.Password;
            server.PasswordFormat = PasswordFormats.Dpapi;
        }

        return server;
    }

    private static EncryptedPassword Encrypt(string plainText, byte[] key)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plain = Encoding.Unicode.GetBytes(plainText);
        try
        {
            var ciphertext = new byte[plain.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, ciphertext, tag);
            return new EncryptedPassword
            {
                Nonce = Convert.ToBase64String(nonce),
                Ciphertext = Convert.ToBase64String(ciphertext),
                Tag = Convert.ToBase64String(tag),
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static string Decrypt(EncryptedPassword encrypted, byte[] key)
    {
        byte[] nonce = Convert.FromBase64String(encrypted.Nonce);
        byte[] ciphertext = Convert.FromBase64String(encrypted.Ciphertext);
        byte[] tag = Convert.FromBase64String(encrypted.Tag);
        var plain = new byte[ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plain);
            return Encoding.Unicode.GetString(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static string? TryUnprotect(string password)
    {
        try
        {
            return DpapiService.Unprotect(password);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
