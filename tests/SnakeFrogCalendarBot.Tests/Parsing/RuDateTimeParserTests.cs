using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Infrastructure.Parsing;

namespace SnakeFrogCalendarBot.Tests.Parsing;

public sealed class RuDateTimeParserTests
{
    private sealed class TestClock : IClock
    {
        private readonly DateTime _utcNow;

        public TestClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;
    }

    private sealed class TestTimeZoneProvider : ITimeZoneProvider
    {
        private readonly string _timeZoneId;

        public TestTimeZoneProvider(string timeZoneId = "Europe/Moscow")
        {
            _timeZoneId = timeZoneId;
        }

        public string GetTimeZoneId() => _timeZoneId;
    }

    [Test]
    public void TryParse_WithFullDateTime_ReturnsCorrectResult()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("2026-01-07 20:00", out var parseResult);

        Assert.That(result, Is.True);
        Assert.That(parseResult, Is.Not.Null);
        Assert.That(parseResult!.Year, Is.EqualTo(2026));
        Assert.That(parseResult.Month, Is.EqualTo(1));
        Assert.That(parseResult.Day, Is.EqualTo(7));
        Assert.That(parseResult.Hour, Is.EqualTo(20));
        Assert.That(parseResult.Minute, Is.EqualTo(0));
        Assert.That(parseResult.HasYear, Is.True);
    }

    [Test]
    public void TryParse_WithDateOnly_ReturnsCorrectResult()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("2026-01-07", out var parseResult);

        Assert.That(result, Is.True);
        Assert.That(parseResult, Is.Not.Null);
        Assert.That(parseResult!.Year, Is.EqualTo(2026));
        Assert.That(parseResult.Month, Is.EqualTo(1));
        Assert.That(parseResult.Day, Is.EqualTo(7));
        Assert.That(parseResult.Hour, Is.Null);
        Assert.That(parseResult.Minute, Is.Null);
        Assert.That(parseResult.HasYear, Is.True);
    }

    [Test]
    public void TryParse_WithRussianDateWithYear_ReturnsCorrectResult()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("7 января 2026", out var parseResult);

        Assert.That(result, Is.True);
        Assert.That(parseResult, Is.Not.Null);
        Assert.That(parseResult!.Year, Is.EqualTo(2026));
        Assert.That(parseResult.Month, Is.EqualTo(1));
        Assert.That(parseResult.Day, Is.EqualTo(7));
        Assert.That(parseResult.HasYear, Is.True);
    }

    [Test]
    public void TryParse_WithRussianDateWithoutYear_ReturnsNextOccurrence()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("7 января", out var parseResult);

        Assert.That(result, Is.True);
        Assert.That(parseResult, Is.Not.Null);
        Assert.That(parseResult!.Year, Is.EqualTo(2026));
        Assert.That(parseResult.Month, Is.EqualTo(1));
        Assert.That(parseResult.Day, Is.EqualTo(7));
        Assert.That(parseResult.HasYear, Is.False);
    }

    [Test]
    public void TryParse_WithRussianDateWithoutYear_PastDate_ReturnsNextYear()
    {
        var clock = new TestClock(new DateTime(2026, 2, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("7 января", out var parseResult);

        Assert.That(result, Is.True);
        Assert.That(parseResult, Is.Not.Null);
        Assert.That(parseResult!.Year, Is.EqualTo(2027));
        Assert.That(parseResult.Month, Is.EqualTo(1));
        Assert.That(parseResult.Day, Is.EqualTo(7));
        Assert.That(parseResult.HasYear, Is.False);
    }

    [Test]
    public void TryParse_WithTimeOnly_ReturnsCorrectResult()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("20:00", out var parseResult);

        Assert.That(result, Is.True);
        Assert.That(parseResult, Is.Not.Null);
        Assert.That(parseResult!.Hour, Is.EqualTo(20));
        Assert.That(parseResult.Minute, Is.EqualTo(0));
        Assert.That(parseResult.HasYear, Is.False);
    }

    [Test]
    public void TryParse_WithInvalidInput_ReturnsFalse()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("invalid input", out var parseResult);

        Assert.That(result, Is.False);
        Assert.That(parseResult, Is.Null);
    }

    [Test]
    public void TryParse_WithEmptyInput_ReturnsFalse()
    {
        var clock = new TestClock(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
        var timeZoneProvider = new TestTimeZoneProvider();
        var parser = new RuDateTimeParser(clock, timeZoneProvider);

        var result = parser.TryParse("", out var parseResult);

        Assert.That(result, Is.False);
        Assert.That(parseResult, Is.Null);
    }
}