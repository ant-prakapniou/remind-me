namespace RemindMe.Core;

public sealed class ReminderService
{
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(1);

    private readonly NoteStore _store;
    private readonly LocalTime _localTime;

    public ReminderService(NoteStore store, LocalTime localTime)
    {
        _store = store;
        _localTime = localTime;
    }

    public string Create(long userId, string input)
    {
        var (text, dtLocal) = DateTimeTailParser.Parse(input);
        var dtUtc = _localTime.ToUtc(dtLocal);

        if (dtUtc < DateTime.UtcNow - PastTolerance)
            throw new ReminderInPastException(dtLocal);

        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            OwnerUserId = userId,
            Text = text ?? string.Empty,
            RemindAtUtc = dtUtc
        };

        var notes = _store.Load();
        notes.Add(note);
        _store.Save(notes);

        var when = dtLocal.ToString("yyyy-MM-dd HH:mm");
        return Messages.ReminderSet(note.Text, when);
    }
}