namespace RemindMe.Core;

public sealed class ReminderService
{
    private readonly NoteStore _store;
    private readonly LocalTime _localTime;

    public ReminderService(NoteStore store, LocalTime localTime)
    {
        _store = store;
        _localTime = localTime;
    }

    /// <summary>
    /// Parses input, creates a note, saves it, and returns the reply text.
    /// Throws <see cref="FormatException"/> if input cannot be parsed.
    /// </summary>
    public string Create(long userId, string input)
    {
        var (text, dtLocal) = DateTimeTailParser.Parse(input);

        var note = new Note
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            OwnerUserId = userId,
            Text = text ?? string.Empty,
            RemindAtUtc = _localTime.ToUtc(dtLocal)
        };

        var notes = _store.Load();
        notes.Add(note);
        _store.Save(notes);

        var when = dtLocal.ToString("yyyy-MM-dd HH:mm");
        return Messages.ReminderSet(note.Text, when);
    }
}