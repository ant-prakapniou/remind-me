namespace RemindMe.Core;

/// <summary>
/// Converts between the user's local time zone and UTC.
/// </summary>
/// <remarks>
/// <para>
/// The bot stores all reminder times in UTC (see <see cref="Note.RemindAtUtc"/>),
/// while users enter and read times in their local zone. This class performs
/// the conversion in both directions using a fixed hour offset.
/// </para>
/// <para>
/// A fixed offset is used instead of <see cref="TimeZoneInfo"/> because the
/// target zone (Belarus, UTC+3) does not observe daylight saving time, and
/// fixed offsets work identically on Windows, Linux, and minimal Docker
/// containers without <c>tzdata</c>. If the bot ever needs to support a zone
/// with DST, this class should be replaced with a <see cref="TimeZoneInfo"/>-based
/// implementation.
/// </para>
/// </remarks>
/// <param name="offsetHours">
/// The local time zone offset from UTC in hours. For Minsk (Belarus) this is <c>3</c>.
/// </param>
public sealed class LocalTime(int offsetHours)
{
    /// <summary>
    /// The local time zone offset from UTC in hours.
    /// </summary>
    /// <remarks>
    /// Configured via <c>LocalTime:OffsetHours</c> in <c>appsettings.json</c>.
    /// Defaults to <c>3</c> if not specified.
    /// </remarks>
    public int OffsetHours { get; } = offsetHours;

    /// <summary>
    /// Converts a local date and time to UTC.
    /// </summary>
    /// <param name="local">
    /// A local date and time. The <see cref="DateTime.Kind"/> of the input
    /// is ignored; only the value is used.
    /// </param>
    /// <returns>
    /// The same moment in UTC, with <see cref="DateTime.Kind"/> set to
    /// <see cref="DateTimeKind.Utc"/>.
    /// </returns>
    /// <example>
    /// <code>
    /// var lt = new LocalTime(3);
    /// var utc = lt.ToUtc(new DateTime(2026, 6, 21, 17, 0, 0));
    /// // utc == 2026-06-21 14:00:00 UTC
    /// </code>
    /// </example>
    public DateTime ToUtc(DateTime local) =>
        DateTime.SpecifyKind(local.AddHours(-OffsetHours), DateTimeKind.Utc);

    /// <summary>
    /// Converts a UTC date and time to local time.
    /// </summary>
    /// <param name="utc">A date and time in UTC.</param>
    /// <returns>
    /// The same moment in local time. The returned value has
    /// <see cref="DateTimeKind.Unspecified"/>, because .NET has no
    /// <c>Local</c> kind for a fixed non-system offset.
    /// </returns>
    /// <example>
    /// <code>
    /// var lt = new LocalTime(3);
    /// var local = lt.ToLocal(new DateTime(2026, 6, 21, 14, 0, 0, DateTimeKind.Utc));
    /// // local == 2026-06-21 17:00:00
    /// </code>
    /// </example>
    public DateTime ToLocal(DateTime utc) => utc.AddHours(OffsetHours);
}