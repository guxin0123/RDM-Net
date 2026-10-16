using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RdmNet.Models;
using RdmNet.Utils;

namespace RdmNet.Services;

/// <summary>connections.json 的读写。</summary>
public sealed class StorageService
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ConnectionsFile Load()
    {
        string path = AppPaths.ConnectionsFile;
        if (!File.Exists(path))
        {
            var empty = new ConnectionsFile();
            ConnectionData.EnsureDefaultGroup(empty);
            return empty;
        }

        string json = File.ReadAllText(path);
        ConnectionsFile? data;
        try
        {
            data = JsonSerializer.Deserialize<ConnectionsFile>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"connections.json 格式不正确：{ex.Message}", ex);
        }

        data ??= new ConnectionsFile();
        ConnectionData.EnsureDefaultGroup(data);
        return data;
    }

    public void Save(ConnectionsFile data)
    {
        string json = JsonSerializer.Serialize(data, SerializerOptions);
        WriteAtomic(AppPaths.ConnectionsFile, json);
    }

    /// <summary>先写临时文件再替换，避免写入中途失败损坏原文件。</summary>
    public static void WriteAtomic(string path, string content)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, content);
        File.Move(temporary, path, overwrite: true);
    }
}
