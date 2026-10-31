namespace RemindMe.Core;

/// <summary>
/// Thrown by <see cref="ReminderService.Create"/> when the requested
/// reminder time is in the past.
/// </summary>
/// <remarks>
/// <para>
/// This is a business-rule violation, not a parse error: the input was
/// valid, but the resulting moment is older than the current time
/// (with a small tolerance, see <see cref="ReminderService"/>).
/// The bot catches this exception separately from <see cref="FormatException"/>
/// so it can show a specific message instead of the generic parse hint.
/// </para>
/// <para>
/// The exception carries the local date and time that was rejected, so the
/// bot can include it in the user-facing message without re-parsing the input.
/// </para>
/// </remarks>
/// <param name="localDateTime">
/// The local date and time that the user requested, in the user's time zone
/// (not UTC). Used both for the exception message and for
/// <see cref="LocalDateTime"/>.
/// </param>
public sealed class ReminderInPastException(DateTime localDateTime)
    : Exception($"Reminder time is in the past: {localDateTime:yyyy-MM-dd HH:mm}")
{
    /// <summary>
    /// The local date and time that was rejected because it is in the past.
    /// </summary>
    /// <remarks>
    /// This is the value in the user's local time zone (e.g. UTC+3 for Minsk),
    /// not UTC. The bot uses it to show the rejected time back to the user.
    /// </remarks>
    public DateTime LocalDateTime { get; } = localDateTime;
}