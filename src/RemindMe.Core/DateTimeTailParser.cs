using System.Globalization;
using System.Text.RegularExpressions;

namespace RemindMe.Core;

public static class DateTimeTailParser
{
    private static readonly Regex DigitRun = new(@"\d+", RegexOptions.Compiled);

    public static (string? Message, DateTime DateTime) Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var runs = DigitRun.Matches(input);
        if (runs.Count < 5)
            throw new FormatException(
                $"Ожидалось минимум 5 групп цифр (HH mm dd MM yyyy), найдено {runs.Count}.");

        var yyyy = ParseToken(runs[^1], "yyyy", 4);
        var MM = ParseToken(runs[^2], "MM", 2);
        var dd = ParseToken(runs[^3], "dd", 2);
        var mm = ParseToken(runs[^4], "mm", 2);
        var hh = ParseToken(runs[^5], "HH", 2);

        if (hh is < 0 or > 23) throw new FormatException($"Часы вне 00..23: {hh}.");
        if (mm is < 0 or > 59) throw new FormatException($"Минуты вне 00..59: {mm}.");

        DateTime dtLocal;
        try
        {
            dtLocal = new DateTime(yyyy, MM, dd, hh, mm, 0, DateTimeKind.Unspecified);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException("Невалидная дата/время.", ex);
        }

        var message = input[..runs[^5].Index].TrimEnd();
        return (string.IsNullOrWhiteSpace(message) ? null : message, dtLocal);
    }

    private static int ParseToken(Match run, string name, int expectedLength)
    {
        var span = run.Value;
        if (span.Length != expectedLength)
            throw new FormatException(
                $"Токен {name} должен содержать ровно {expectedLength} цифр, получено '{span}'.");

        return int.Parse(span, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}