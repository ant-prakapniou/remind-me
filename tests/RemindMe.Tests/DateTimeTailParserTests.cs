using RemindMe.Core;

namespace RemindMe.Tests;

public class DateTimeTailParserTests
{
    [Fact]
    public void Parses_TextWithDateTime()
    {
        var (message, dt) = DateTimeTailParser.Parse("buy bread 17 00 21 06 2026");
        Assert.Equal("buy bread", message);
        Assert.Equal(new DateTime(2026, 6, 21, 17, 0, 0, DateTimeKind.Unspecified), dt);
    }

    [Fact]
    public void Parses_DateTimeOnly_ReturnsNullMessage()
    {
        var (message, dt) = DateTimeTailParser.Parse("14:30 25.12.2025");
        Assert.Null(message);
        Assert.Equal(new DateTime(2025, 12, 25, 14, 30, 0), dt);
    }

    [Fact]
    public void Parses_WithVariousSeparators()
    {
        var (message, dt) = DateTimeTailParser.Parse("call mom 09 05 01--02--2024");
        Assert.Equal("call mom", message);
        Assert.Equal(new DateTime(2024, 2, 1, 9, 5, 0), dt);
    }

    [Fact]
    public void KeepsExtraDigitsInText()
    {
        var (message, _) = DateTimeTailParser.Parse("buy 2 bread 3 pieces 09 05 01 02 2024");
        Assert.Equal("buy 2 bread 3 pieces", message);
    }

    [Fact]
    public void Throws_OnInvalidDate()
    {
        Assert.Throws<FormatException>(() =>
            DateTimeTailParser.Parse("10 00 30 02 2026"));
    }

    [Fact]
    public void Throws_OnInvalidDate_April31()
    {
        Assert.Throws<FormatException>(() =>
            DateTimeTailParser.Parse("09 00 31 04 2026"));
    }

    [Fact]
    public void Throws_OnShortYear()
    {
        Assert.Throws<FormatException>(() =>
            DateTimeTailParser.Parse("buy bread 17 00 21 06 26"));
    }

    [Fact]
    public void Throws_OnTooFewGroups()
    {
        Assert.Throws<FormatException>(() =>
            DateTimeTailParser.Parse("no date here"));
    }

    [Fact]
    public void Throws_OnNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DateTimeTailParser.Parse(null!));
    }
}