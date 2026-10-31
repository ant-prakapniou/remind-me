using System.Text.Json;

namespace RemindMe.Core;

/// <summary>
/// Reads and writes a set of <see cref="long"/> values to a JSON file on disk.
/// </summary>
/// <remarks>
/// <para>
/// Used for both <c>users.json</c> (approved users) and <c>pending.json</c>
/// (users awaiting approval). The JSON representation is a plain array of
/// numbers, e.g. <c>[6176661225, 123456789]</c>.
/// </para>
/// <para>
/// Writes are atomic: content goes to <c>{filePath}.tmp</c> first, then the
/// temporary file replaces the original via
/// <see cref="File.Move(string, string, bool)"/>. If the process crashes
/// mid-write, the original file remains intact.
/// </para>
/// <para>
/// The store is not thread-safe. In the current deployment the bot is the
/// only writer of these files, so no locking is needed. The cron job does
/// not touch user or pending lists.
/// </para>
/// </remarks>
/// <param name="filePath">
/// Absolute or relative path to the JSON file. Parent directories are created
/// automatically on save. On Railway this is <c>/data/users.json</c> or
/// <c>/data/pending.json</c> (a mounted volume).
/// </param>
public sealed class JsonLongSetStore(string filePath)
{
    private readonly string _filePath = filePath;

    /// <summary>
    /// Loads the set of IDs from the file.
    /// </summary>
    /// <returns>
    /// A set of IDs. Returns an empty set if the file does not exist, is empty,
    /// or contains only whitespace. Never returns <see langword="null"/>.
    /// </returns>
    /// <exception cref="JsonException">
    /// The file exists and is not empty, but its content is not valid JSON
    /// or is not an array of numbers.
    /// </exception>
    /// <exception cref="IOException">
    /// The file exists but cannot be read (permissions, locked by another process).
    /// </exception>
    public HashSet<long> Load()
    {
        if (!File.Exists(_filePath)) return [];
        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<HashSet<long>>(json) ?? [];
    }

    /// <summary>
    /// Writes the given set of IDs to the file, replacing any previous content.
    /// </summary>
    /// <param name="ids">The full set of IDs to persist.</param>
    /// <remarks>
    /// <para>
    /// The write is atomic: content goes to <c>{filePath}.tmp</c> first, then
    /// the temporary file replaces the original. A reader never sees a
    /// partially written file.
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
    public void Save(HashSet<long> ids)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(ids));
        File.Move(tmp, _filePath, overwrite: true);
    }
}