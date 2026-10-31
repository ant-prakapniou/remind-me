namespace RemindMe.Core;

public sealed class LocalTime(int offsetHours)
{
    public int OffsetHours { get; } = offsetHours;

    public DateTime ToUtc(DateTime local) =>
        DateTime.SpecifyKind(local.AddHours(-OffsetHours), DateTimeKind.Utc);

    public DateTime ToLocal(DateTime utc) => utc.AddHours(OffsetHours);
}