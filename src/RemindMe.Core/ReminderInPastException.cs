namespace RemindMe.Core;

public sealed class ReminderInPastException(DateTime localDateTime) : Exception($"Reminder time is in the past: {localDateTime:yyyy-MM-dd HH:mm}")
{
    public DateTime LocalDateTime { get; } = localDateTime;
}