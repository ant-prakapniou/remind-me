using RemindMe.Core;

namespace RemindMe.Tests;

public class LocalTimeTests
{
    [Fact]
    public void ToUtc_SubtractsOffset()
    {
        var lt = new LocalTime(3);
        var utc = lt.ToUtc(new DateTime(2026, 6, 21, 17, 0, 0));

        Assert.Equal(new DateTime(2026, 6, 21, 14, 0, 0), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void ToLocal_AddsOffset()
    {
        var lt = new LocalTime(3);
        var local = lt.ToLocal(new DateTime(2026, 6, 21, 14, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 6, 21, 17, 0, 0), local);
    }

    [Fact]
    public void ToUtc_HandlesDayBoundary()
    {
        var lt = new LocalTime(3);
        var utc = lt.ToUtc(new DateTime(2026, 6, 21, 1, 0, 0));

        Assert.Equal(new DateTime(2026, 6, 20, 22, 0, 0), utc);
    }

    [Fact]
    public void RoundTrip_PreservesValue()
    {
        var lt = new LocalTime(3);
        var original = new DateTime(2026, 6, 21, 17, 30, 0);

        var utc = lt.ToUtc(original);
        var back = lt.ToLocal(utc);

        Assert.Equal(original, back);
    }

    [Fact]
    public void ZeroOffset_NoChange()
    {
        var lt = new LocalTime(0);
        var dt = new DateTime(2026, 6, 21, 17, 0, 0);
        Assert.Equal(dt, lt.ToUtc(dt));
    }
}