using System.Text.Json;

namespace RemindMe.Core;

/// <summary>
/// Reads and writes the list of notes to a JSON file on disk.
/// </summary>
/// <remarks>
/// <para>
/// The store keeps the entire note list in a single JSON file (one array of
/// <see cref="Note"/> objects). This is intentionally simple: the target
/// scale is dozens to a few hundred notes owned by a handful of users, and
/// the JSON file can be inspected or edited by hand if needed.
/// </para>
/// <para>
/// Writes are atomic: the new content is first written to a temporary file
/// (<c>{filePath}.tmp</c>) and then moved over the original with
/// <see cref="File.Move(string, string, bool)"/>. If the process crashes
/// mid-write, the original file remains intact and the temporary file is
/// left behind — it will be overwritten on the next successful save.
/// </para>
/// <para>
/// The store is not thread-safe. It assumes a single writer at a time,
/// which holds for the current deployment: the bot writes notes when users
/// create them, and the cron job writes notes after sending them. The two
/// services do not run concurrently by design (cron completes before the
/// next scheduled run).
/// </para>
/// </remarks>
/// <param name="filePath">
/// Absolute or relative path to the JSON file. Parent directories are
/// created automatically on save if they do not exist.
/// On Railway this is <c>/data/notes.json</c> (a mounted volume).
/// </param>
public sealed class NoteStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _filePath = filePath;

    /// <summary>
    /// Loads all notes from the file.
    /// </summary>
    /// <returns>
    /// A list of notes. Returns an empty list if the file does not exist,
    /// is empty, or contains only whitespace. Never returns <see langword="null"/>.
    /// </returns>
    /// <exception cref="JsonException">
    /// The file exists and is not empty, but its content is not valid JSON
    /// or does not match the <see cref="Note"/> schema.
    /// </exception>
    /// <exception cref="IOException">
    /// The file exists but cannot be read (permissions, locked by another process).
    /// </exception>
    public List<Note> Load()
    {
        if (!File.Exists(_filePath)) return [];
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return new List<Note>();
        return JsonSerializer.Deserialize<List<Note>>(json, JsonOpts) ?? [];
    }

    /// <summary>
    /// Writes the given notes to the file, replacing any previous content.
    /// </summary>
    /// <param name="notes">The full list of notes to persist.</param>
    /// <remarks>
    /// <para>
    /// The write is atomic: content goes to <c>{filePath}.tmp</c> first,
    /// then the temporary file replaces the original. A reader never sees
    /// a partially written file.
    /// </para>
    /// <para>
    /// If the parent directory does not exist, it is created.
    /// </para>
    /// </remarks>
    /// <exception cref="IOException">
    /// The temporary file cannot be written, or the move fails
    /// (e.g. the volume is read-only or full).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The process lacks permission to write to the target directory.
    /// </exception>
    public void Save(List<Note> notes)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(notes, JsonOpts));
        File.Move(tmp, _filePath, overwrite: true);
    }
}