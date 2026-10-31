namespace RemindMe.Core;

/// <summary>
/// Creates and persists reminders from raw user input.
/// </summary>
/// <remarks>
/// <para>
/// This is the core business logic of the bot, independent of Telegram.
/// It parses the input string via <see cref="DateTimeTailParser"/>,
/// validates that the resulting moment is not in the past, converts it to
/// UTC via <see cref="LocalTime"/>, creates a <see cref="Note"/>, stores it
/// via <see cref="NoteStore"/>, and returns the confirmation text for the user.
/// </para>
/// <para>
/// The service does not send anything. The caller (the bot) is responsible
/// for delivering the returned string and for catching the exceptions below.
/// This separation makes the service testable without Telegram.
/// </para>
/// </remarks>
public sealed class ReminderService
{
    /// <summary>
    /// Tolerance applied when rejecting past dates.
    /// </summary>
    /// <remarks>
    /// A reminder set to "now" or a few seconds ago is still accepted, because
    /// by the time the user submits the message the clock may have advanced
    /// past the minute they intended. Only dates older than this tolerance
    /// are rejected.
    /// </remarks>
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(1);

    private readonly NoteStore _store;
    private readonly LocalTime _localTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderService"/> class.
    /// </summary>
    /// <param name="store">The store used to persist notes.</param>
    /// <param name="localTime">The converter between the user's local time and UTC.</param>
    public ReminderService(NoteStore store, LocalTime localTime)
    {
        _store = store;
        _localTime = localTime;
    }

    /// <summary>
    /// Parses the input, creates a new reminder, saves it, and returns the reply text.
    /// </summary>
    /// <param name="userId">Telegram user ID of the note's owner.</param>
    /// <param name="input">
    /// Raw user input in the format <c>&lt;text&gt; HH MM DD MM YYYY</c>.
    /// See <see cref="DateTimeTailParser"/> for the exact rules.
    /// </param>
    /// <returns>
    /// A confirmation message ready to be sent to the user, containing the
    /// note text (if any) and the scheduled local time.
    /// </returns>
    /// <exception cref="FormatException">
    /// The input does not match the expected format. See
    /// <see cref="DateTimeTailParser.Parse"/> for details.
    /// </exception>
    /// <exception cref="ReminderInPastException">
    /// The parsed date and time is more than one minute in the past.
    /// </exception>
    /// <exception cref="IOException">
    /// The note could not be persisted to disk.
    /// </exception>
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