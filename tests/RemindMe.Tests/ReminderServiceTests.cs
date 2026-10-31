using RemindMe.Core;

namespace RemindMe.Tests;

public class ReminderServiceTests : IDisposable
{
    private readonly string _path;
    private readonly NoteStore _store;
    private readonly ReminderService _service;

    public ReminderServiceTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
        _store = new NoteStore(_path);
        _service = new ReminderService(_store, new LocalTime(3));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_SavesNoteAndReturnsReply()
    {
        var reply = _service.Create(42, "buy bread 17 00 21 06 2026");

        Assert.Contains("buy bread", reply);
        Assert.Contains("2026-06-21 17:00", reply);

        var notes = _store.Load();
        Assert.Single(notes);
        Assert.Equal(42, notes[0].OwnerUserId);
        Assert.Equal("buy bread", notes[0].Text);
        Assert.Equal(new DateTime(2026, 6, 21, 14, 0, 0, DateTimeKind.Utc), notes[0].RemindAtUtc);
    }

    [Fact]
    public void Create_EmptyText_SavesEmptyString()
    {
        var reply = _service.Create(42, "17 00 21 06 2026");

        Assert.DoesNotContain("\"", reply);
        var notes = _store.Load();
        Assert.Single(notes);
        Assert.Equal("", notes[0].Text);
    }

    [Fact]
    public void Create_InvalidInput_Throws()
    {
        Assert.Throws<FormatException>(() => _service.Create(42, "no date"));
        Assert.Empty(_store.Load());
    }
}