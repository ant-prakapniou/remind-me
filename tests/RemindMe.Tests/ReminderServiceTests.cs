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
        var future = DateTime.UtcNow.AddHours(3).AddDays(1); // завтра, +3 — гарантированно в будущем
        var input = $"buy bread {future:HH mm dd MM yyyy}";

        var reply = _service.Create(42, input);

        Assert.Contains("buy bread", reply);
        Assert.Contains($"{future:yyyy-MM-dd HH:mm}", reply);

        var notes = _store.Load();
        Assert.Single(notes);
        Assert.Equal(42, notes[0].OwnerUserId);
        Assert.Equal("buy bread", notes[0].Text);
    }

    [Fact]
    public void Create_EmptyText_SavesEmptyString()
    {
        var future = DateTime.UtcNow.AddHours(3).AddDays(1);
        var input = $"{future:HH mm dd MM yyyy}";

        var reply = _service.Create(42, input);

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
    [Fact]
    public void Create_PastDate_Throws()
    {
        var ex = Assert.Throws<ReminderInPastException>(
            () => _service.Create(42, "buy bread 17 00 21 06 2020"));
        Assert.Equal(new DateTime(2020, 6, 21, 17, 0, 0), ex.LocalDateTime);
        Assert.Empty(_store.Load());
    }

    [Fact]
    public void Create_WithinPastTolerance_Succeeds()
    {
        // 30 seconds ago in "Minsk" time, converted through the service's LocalTime(3)
        var nowUtc = DateTime.UtcNow;
        var local = nowUtc.AddHours(3).AddSeconds(-30);
        var input = $"test {local:HH mm dd MM yyyy}";
        var reply = _service.Create(42, input);
        Assert.Contains("reminder set", reply);
    }
}