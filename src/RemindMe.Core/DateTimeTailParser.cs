using System.Globalization;
using System.Text.RegularExpressions;

namespace RemindMe.Core;

/// <summary>
/// Parses a message whose tail contains five digit groups representing
/// a date and time: <c>[text] HH mm dd MM yyyy</c>.
/// </summary>
/// <remarks>
/// <para>
/// The parser scans the input from the end, collecting digit groups
/// (maximal runs of <c>0-9</c>) separated by any non-digit characters.
/// It stops after collecting exactly five groups. Everything to the left
/// of the fifth group is returned as the message text.
/// </para>
/// <para>
/// Group positions are fixed relative to the end of the string:
/// <list type="number">
///   <item><description><c>runs[^1]</c> — year, exactly 4 digits (<c>yyyy</c>);</description></item>
///   <item><description><c>runs[^2]</c> — month, exactly 2 digits (<c>MM</c>);</description></item>
///   <item><description><c>runs[^3]</c> — day, exactly 2 digits (<c>dd</c>);</description></item>
///   <item><description><c>runs[^4]</c> — minutes, exactly 2 digits (<c>mm</c>);</description></item>
///   <item><description><c>runs[^5]</c> — hours, exactly 2 digits (<c>HH</c>, 24-hour format);</description></item>
///   <item><description>everything left of <c>runs[^5]</c> — the message text.</description></item>
/// </list>
/// </para>
/// <para>
/// Separators between digit groups can be any non-digit characters
/// (space, dot, dash, colon, multiple characters in a row, etc.).
/// Adjacent groups must be separated — if two groups would merge into one
/// continuous digit run, the boundary cannot be determined and the parser
/// throws <see cref="FormatException"/>.
/// </para>
/// <para>
/// The returned <see cref="DateTime"/> has <see cref="DateTimeKind.Unspecified"/>.
/// The parser knows nothing about time zones; conversion to UTC is the
/// caller's responsibility (see <see cref="LocalTime.ToUtc"/>).
/// Seconds and milliseconds are always zero.
/// </para>
/// </remarks>
public static class DateTimeTailParser
{
    private static readonly Regex DigitRun = new(@"\d+", RegexOptions.Compiled);

    /// <summary>
    /// Parses a string whose tail contains <c>HH mm dd MM yyyy</c>.
    /// </summary>
    /// <param name="input">The input string. Must not be <see langword="null"/>.</param>
    /// <returns>
    /// A tuple:
    /// <list type="bullet">
    ///   <item><description><c>Message</c> — text before the date/time tail,
    ///   or <see langword="null"/> if the text is empty or whitespace;</description></item>
    ///   <item><description><c>DateTime</c> — the parsed date and time with
    ///   <see cref="DateTimeKind.Unspecified"/>.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// The string does not match the expected format: fewer than five digit
    /// groups; a token has the wrong length (4/2/2/2/2); hours outside
    /// 00..23; minutes outside 00..59; or the date does not exist
    /// (e.g. February 30, April 31, February 29 in a non-leap year).
    /// </exception>
    /// <example>
    /// <code>
    /// var (msg, dt) = DateTimeTailParser.Parse("buy bread 17 00 21 06 2026");
    /// // msg == "buy bread", dt == 2026-06-21 17:00:00
    ///
    /// var (msg2, dt2) = DateTimeTailParser.Parse("14:30 25.12.2025");
    /// // msg2 == null, dt2 == 2025-12-25 14:30:00
    ///
    /// var (msg3, dt3) = DateTimeTailParser.Parse("call mom 09 05 01--02--2024");
    /// // msg3 == "call mom", dt3 == 2024-02-01 09:05:00
    /// </code>
    /// </example>
    public static (string? Message, DateTime DateTime) Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var runs = DigitRun.Matches(input);
        if (runs.Count < 5)
            throw new FormatException(
                $"Expected at least 5 digit groups (HH mm dd MM yyyy), found {runs.Count}.");

        var yyyy = ParseToken(runs[^1], "yyyy", 4);
        var MM = ParseToken(runs[^2], "MM", 2);
        var dd = ParseToken(runs[^3], "dd", 2);
        var mm = ParseToken(runs[^4], "mm", 2);
        var hh = ParseToken(runs[^5], "HH", 2);

        if (hh is < 0 or > 23) throw new FormatException($"Hours out of range 00..23: {hh}.");
        if (mm is < 0 or > 59) throw new FormatException($"Minutes out of range 00..59: {mm}.");

        DateTime dtLocal;
        try
        {
            dtLocal = new DateTime(yyyy, MM, dd, hh, mm, 0, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException("Invalid date or time.", ex);
        }

        var message = input[..runs[^5].Index].TrimEnd();
        return (string.IsNullOrWhiteSpace(message) ? null : message, dtLocal);
    }

    /// <summary>
    /// Parses a single digit group into an <see cref="int"/>, validating its exact length.
    /// </summary>
    /// <param name="run">A regex match containing a continuous run of digits.</param>
    /// <param name="name">Token name for the error message (e.g. <c>"HH"</c>).</param>
    /// <param name="expectedLength">Expected number of digits in the token.</param>
    /// <returns>The numeric value of the token.</returns>
    /// <exception cref="FormatException">
    /// The token length does not equal <paramref name="expectedLength"/>.
    /// </exception>
    private static int ParseToken(Match run, string name, int expectedLength)
    {
        var span = run.Value;
        if (span.Length != expectedLength)
            throw new FormatException(
                $"Token {name} must contain exactly {expectedLength} digits, got '{span}'.");

        return int.Parse(span, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}