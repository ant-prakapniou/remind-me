using RemindMe.Core;

namespace RemindMe.Tests;

public class NoteStoreTests : IDisposable
{
    private readonly string _path;

    public NoteStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_ReturnsEmpty_WhenFileMissing()
    {
        var store = new NoteStore(_path);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var store = new NoteStore(_path);
        var note = new Note
        {
            Id = "abc12345",
            OwnerUserId = 42,
            Text = "hello",
            RemindAtUtc = new DateTime(2026, 6, 21, 14, 0, 0, DateTimeKind.Utc)
        };

        store.Save([note]);
        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("abc12345", loaded[0].Id);
        Assert.Equal(42, loaded[0].OwnerUserId);
        Assert.Equal("hello", loaded[0].Text);
        Assert.Equal(note.RemindAtUtc, loaded[0].RemindAtUtc);
    }

    [Fact]
    public void Save_OverwritesExisting()
    {
        var store = new NoteStore(_path);
        store.Save([MakeNote("1")]);
        store.Save([MakeNote("2")]);

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("2", loaded[0].Id);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"remind_{Guid.NewGuid():N}");
        var path = Path.Combine(dir, "notes.json");
        try
        {
            new NoteStore(path).Save(new List<Note> { MakeNote("1") });
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    private static Note MakeNote(string id) => new()
    {
        Id = id,
        OwnerUserId = 1,
        Text = "",
        RemindAtUtc = DateTime.UtcNow
    };
}