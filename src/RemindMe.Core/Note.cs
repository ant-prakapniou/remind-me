namespace RemindMe.Core;

public sealed class Note
{
    public required string Id { get; init; }
    public required long OwnerUserId { get; init; }
    public required string Text { get; init; }
    public required DateTime RemindAtUtc { get; init; }
}