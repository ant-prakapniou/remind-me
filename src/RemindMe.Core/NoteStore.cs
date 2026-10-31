using System.Text.Json;

namespace RemindMe.Core;

public sealed class NoteStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _filePath;

    public NoteStore(string filePath)
    {
        _filePath = filePath;
    }

    public List<Note> Load()
    {
        if (!File.Exists(_filePath)) return [];
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<Note>();
        return JsonSerializer.Deserialize<List<Note>>(json, JsonOpts) ?? [];
    }

    public void Save(List<Note> notes)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(notes, JsonOpts));
        File.Move(tmp, _filePath, overwrite: true);
    }
}