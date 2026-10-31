namespace RemindMe.Core;

/// <summary>
/// Represents a single reminder stored in the bot's JSON storage.
/// </summary>
/// <remarks>
/// <para>
/// A note is immutable after creation: all properties are <c>init</c>-only.
/// There is intentionally no API to reschedule a note — if the user wants
/// a different time, they create a new note and the old one is removed
/// after delivery.
/// </para>
/// <para>
/// Instances are serialized to and deserialized from <c>notes.json</c> via
/// <see cref="NoteStore"/>. Property names in JSON are camelCase
/// (see <see cref="NoteStore"/> for serializer settings).
/// </para>
/// </remarks>
public sealed class Note
{
    /// <summary>
    /// Short unique identifier of the note (8 hex characters, generated from a GUID).
    /// </summary>
    /// <remarks>
    /// Used by the cron job to track which notes were successfully sent
    /// and should be removed from storage.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Telegram user ID of the note's owner.
    /// </summary>
    /// <remarks>
    /// Every note belongs to exactly one user. The cron job uses this value
    /// as the target chat when sending the reminder, and the bot uses it to
    /// filter notes per user (relevant once more than one user is approved).
    /// </remarks>
    public required long OwnerUserId { get; init; }

    /// <summary>
    /// The reminder text as entered by the user, without the date/time tail.
    /// </summary>
    /// <remarks>
    /// May be an empty string if the user created a "bare alarm" without text.
    /// In that case the delivered message contains only the date and time.
    /// </remarks>
    public required string Text { get; init; }

    /// <summary>
    /// The moment when the reminder should be delivered, in UTC.
    /// </summary>
    /// <remarks>
    /// Always stored in UTC regardless of the user's local time zone.
    /// Conversion from the user's local time (e.g. UTC+3 for Minsk) happens
    /// before the note is created, via <see cref="LocalTime.ToUtc"/>.
    /// The cron job compares this value against <see cref="DateTime.UtcNow"/>
    /// to determine which notes are due.
    /// </remarks>
    public required DateTime RemindAtUtc { get; init; }
}