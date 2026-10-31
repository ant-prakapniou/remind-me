using System.Text.Json;

namespace RemindMe.Core;

public sealed class JsonLongSetStore
{
    private readonly string _filePath;

    public JsonLongSetStore(string filePath) => _filePath = filePath;

    public HashSet<long> Load()
    {
        if (!File.Exists(_filePath)) return [];
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<HashSet<long>>(json) ?? [];
    }

    public void Save(HashSet<long> ids)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(ids));
        File.Move(tmp, _filePath, overwrite: true);
    }
}