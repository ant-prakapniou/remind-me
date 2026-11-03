using Microsoft.Extensions.Time.Testing;
using RemindMe.Core;

namespace RemindMe.Tests;

public class ReminderServiceTests : IDisposable
{
    private readonly string _path;
    private readonly NoteStore _store;
    private readonly FakeTimeProvider _time;
    private readonly ReminderService _service;

    // Fixed "now": 2026-11-03 17:00:30 UTC.
    // With LocalTime(3) this is 20:00:30 local.
    private static readonly DateTimeOffset FixedNow =
        new(2026, 11, 3, 17, 0, 30, TimeSpan.Zero);

    public ReminderServiceTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"notes_{Guid.NewGuid():N}.json");
        _store = new NoteStore(_path);
        _time = new FakeTimeProvider(FixedNow);
        _service = new ReminderService(_store, new LocalTime(3), _time);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_SavesNoteAndReturnsReply()
    {
        // 2026-11-04 20:00 local = 2026-11-04 17:00 UTC — well in the future.
        var input = "buy bread 20 00 04 11 2026";

        var reply = _service.Create(42, input);

        Assert.Contains("buy bread", reply);
        Assert.Contains("2026-11-04 20:00", reply);

        var notes = _store.Load();
        Assert.Single(notes);
        Assert.Equal(42, notes[0].OwnerUserId);
        Assert.Equal("buy bread", notes[0].Text);
    }

    [Fact]
    public void Create_EmptyText_SavesEmptyString()
    {
        var input = "20 00 04 11 2026";

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

    /// <summary>
    /// A reminder 30 seconds in the past (after second truncation) must be accepted.
    /// Input truncates seconds, so "20:00" local maps to 17:00:00 UTC,
    /// which is exactly 30 seconds before the fixed "now" (17:00:30).
    /// Tolerance is 1 minute, so 30 seconds ago is inside the window.
    /// </summary>
    [Fact]
    public void Create_WithinPastTolerance_Succeeds()
    {
        var input = "test 20 00 03 11 2026";

        var reply = _service.Create(42, input);

        Assert.Contains("reminder set", reply);
        var notes = _store.Load();
        Assert.Single(notes);
    }

    /// <summary>
    /// A reminder 90 seconds in the past must be rejected.
    /// "19:59" local maps to 16:59:00 UTC, which is 90 seconds before 17:00:30 UTC —
    /// outside the 1-minute tolerance.
    /// </summary>
    [Fact]
    public void Create_JustOutsidePastTolerance_Throws()
    {
        var input = "test 19 59 03 11 2026";

        Assert.Throws<ReminderInPastException>(() => _service.Create(42, input));
        Assert.Empty(_store.Load());
    }

    /// <summary>
    /// A reminder 150 seconds in the past must be rejected (unambiguous case).
    /// "19:58" local maps to 16:58:00 UTC, which is 150 seconds before 17:00:30 UTC.
    /// </summary>
    [Fact]
    public void Create_WellOutsidePastTolerance_Throws()
    {
        var input = "test 19 58 03 11 2026";

        Assert.Throws<ReminderInPastException>(() => _service.Create(42, input));
        Assert.Empty(_store.Load());
    }
}