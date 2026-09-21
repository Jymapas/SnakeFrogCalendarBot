using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Infrastructure.Parsing;

namespace SnakeFrogCalendarBot.Tests.Parsing;

public sealed class RuRecurrenceParserTests
{
    private const int Monday = 0b0000001;
    private const int Tuesday = 0b0000010;
    private const int Wednesday = 0b0000100;
    private const int Friday = 0b0010000;

    [Test]
    public void TryParse_EveryMonday_ReturnsWeeklyWithSingleDay()
    {
        var result = Parse("каждый понедельник");

        Assert.That(result!.Kind, Is.EqualTo(EventKind.Weekly));
        Assert.That(result.DaysOfWeekMask, Is.EqualTo(Monday));
        Assert.That(result.Hour, Is.Null);
    }

    [Test]
    public void TryParse_EveryTuesdayWithTime_ReturnsWeeklyWithTime()
    {
        var result = Parse("каждый вторник 19:00");

        Assert.That(result!.Kind, Is.EqualTo(EventKind.Weekly));
        Assert.That(result.DaysOfWeekMask, Is.EqualTo(Tuesday));
        Assert.That(result.Hour, Is.EqualTo(19));
        Assert.That(result.Minute, Is.Zero);
    }

    [Test]
    public void TryParse_ShortDayList_ReturnsCombinedMask()
    {
        var result = Parse("по пн, ср, пт");

        Assert.That(result!.Kind, Is.EqualTo(EventKind.Weekly));
        Assert.That(result.DaysOfWeekMask, Is.EqualTo(Monday | Wednesday | Friday));
    }

    [Test]
    public void TryParse_TwoDaysJoinedByAnd_ReturnsCombinedMask()
    {
        var result = Parse("каждые вторник и пятницу");

        Assert.That(result!.DaysOfWeekMask, Is.EqualTo(Tuesday | Friday));
    }

    [Test]
    public void TryParse_EveryTwoWeeks_ReturnsInterval()
    {
        var result = Parse("каждые 2 недели");

        Assert.That(result!.Kind, Is.EqualTo(EventKind.Interval));
        Assert.That(result.IntervalCount, Is.EqualTo(2));
        Assert.That(result.IntervalUnit, Is.EqualTo(RecurrenceUnit.Weeks));
    }

    [Test]
    public void TryParse_OnceInThreeDays_ReturnsInterval()
    {
        var result = Parse("раз в 3 дня");

        Assert.That(result!.IntervalCount, Is.EqualTo(3));
        Assert.That(result.IntervalUnit, Is.EqualTo(RecurrenceUnit.Days));
    }

    [Test]
    public void TryParse_OnceAMonth_DefaultsCountToOne()
    {
        var result = Parse("раз в месяц");

        Assert.That(result!.IntervalCount, Is.EqualTo(1));
        Assert.That(result.IntervalUnit, Is.EqualTo(RecurrenceUnit.Months));
    }

    [Test]
    public void TryParse_EveryDayWithTime_ReturnsDailyInterval()
    {
        var result = Parse("каждый день 08:30");

        Assert.That(result!.IntervalCount, Is.EqualTo(1));
        Assert.That(result.IntervalUnit, Is.EqualTo(RecurrenceUnit.Days));
        Assert.That(result.Hour, Is.EqualTo(8));
        Assert.That(result.Minute, Is.EqualTo(30));
    }

    [Test]
    public void TryParse_EveryTwoYears_ReturnsYearsUnit()
    {
        var result = Parse("каждые 2 года");

        Assert.That(result!.IntervalUnit, Is.EqualTo(RecurrenceUnit.Years));
        Assert.That(result.IntervalCount, Is.EqualTo(2));
    }

    [Test]
    public void TryParse_PlainDate_ReturnsFalse()
    {
        Assert.That(new RuRecurrenceParser().TryParse("7 января 2026", out _), Is.False);
    }

    [Test]
    public void TryParse_EmptyInput_ReturnsFalse()
    {
        Assert.That(new RuRecurrenceParser().TryParse("   ", out _), Is.False);
    }

    [Test]
    public void TryParse_YearlyPhrase_ReturnsFalseSoYearlyKindIsNotHijacked()
    {
        Assert.That(new RuRecurrenceParser().TryParse("ежегодно", out _), Is.False);
    }

    private static RecurrenceParseResult? Parse(string input)
    {
        var parsed = new RuRecurrenceParser().TryParse(input, out var result);
        Assert.That(parsed, Is.True, $"Не распознано: {input}");
        return result;
    }
}
