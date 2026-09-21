using NodaTime;
using SnakeFrogCalendarBot.Application.Recurrence;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.Recurrence;

public sealed class RecurrenceExpanderTests
{
    private static readonly DateTimeZone TimeZone = DateTimeZoneProviders.Tzdb["Europe/Moscow"];
    private static readonly DateTime CreatedAtUtc = new(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);

    // 2026-03-02 — понедельник.
    private static readonly DateOnly WeeklyStart = new(2026, 3, 2);

    private const int Monday = 0b0000001;
    private const int MondayAndWednesday = 0b0000101;

    [Test]
    public void Expand_WeeklyWithTwoDays_ReturnsBothOccurrencesInWeek()
    {
        var eventEntity = Weekly(MondayAndWednesday, WeeklyStart, new TimeSpan(19, 0, 0));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 2), new LocalDate(2026, 3, 8));

        Assert.That(occurrences.Select(o => o.Date), Is.EqualTo(new[]
        {
            new LocalDate(2026, 3, 2),
            new LocalDate(2026, 3, 4)
        }));
        Assert.That(occurrences.All(o => o.Time == new LocalTime(19, 0)), Is.True);
    }

    [Test]
    public void Expand_WeeklyOverMonth_ReturnsEveryMatchingDay()
    {
        var eventEntity = Weekly(MondayAndWednesday, WeeklyStart, null, isAllDay: true);

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(occurrences.Select(o => o.Date.Day), Is.EqualTo(new[] { 2, 4, 9, 11, 16, 18, 23, 25, 30 }));
        Assert.That(occurrences.All(o => o.Time is null), Is.True);
    }

    [Test]
    public void Expand_WeeklyBeforeStartDate_SkipsEarlierDays()
    {
        var eventEntity = Weekly(Monday, new DateOnly(2026, 3, 16), null, isAllDay: true);

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(occurrences.Select(o => o.Date.Day), Is.EqualTo(new[] { 16, 23, 30 }));
    }

    [Test]
    public void Expand_WeeklyWithRepeatUntil_StopsAfterBoundaryInclusive()
    {
        var eventEntity = Weekly(Monday, WeeklyStart, null, isAllDay: true, repeatUntil: new DateOnly(2026, 3, 16));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(occurrences.Select(o => o.Date.Day), Is.EqualTo(new[] { 2, 9, 16 }));
    }

    [Test]
    public void Expand_IntervalInDays_StepsFromStartDate()
    {
        var eventEntity = Interval(3, RecurrenceUnit.Days, new DateOnly(2026, 3, 1));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 8), new LocalDate(2026, 3, 20));

        Assert.That(occurrences.Select(o => o.Date.Day), Is.EqualTo(new[] { 10, 13, 16, 19 }));
    }

    [Test]
    public void Expand_IntervalInWeeks_StepsBySevenDays()
    {
        var eventEntity = Interval(2, RecurrenceUnit.Weeks, new DateOnly(2026, 3, 2));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 4, 30));

        Assert.That(occurrences.Select(o => o.Date), Is.EqualTo(new[]
        {
            new LocalDate(2026, 3, 2),
            new LocalDate(2026, 3, 16),
            new LocalDate(2026, 3, 30),
            new LocalDate(2026, 4, 13),
            new LocalDate(2026, 4, 27)
        }));
    }

    [Test]
    public void Expand_MonthlyIntervalFromEndOfMonth_CountsFromAnchorNotFromPreviousOccurrence()
    {
        // 31 января — anchor. Если считать инкрементально, февраль склампится в 28-е,
        // и март «залипнет» на 28-м вместо 31-го.
        var eventEntity = Interval(1, RecurrenceUnit.Months, new DateOnly(2026, 1, 31));

        var february = Expand(eventEntity, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28));
        var march = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));
        var april = Expand(eventEntity, new LocalDate(2026, 4, 1), new LocalDate(2026, 4, 30));

        Assert.Multiple(() =>
        {
            Assert.That(february.Single().Date, Is.EqualTo(new LocalDate(2026, 2, 28)));
            Assert.That(march.Single().Date, Is.EqualTo(new LocalDate(2026, 3, 31)));
            Assert.That(april.Single().Date, Is.EqualTo(new LocalDate(2026, 4, 30)));
        });
    }

    [Test]
    public void Expand_IntervalEveryTwoMonths_SkipsMonthsBetween()
    {
        var eventEntity = Interval(2, RecurrenceUnit.Months, new DateOnly(2026, 1, 15));

        var february = Expand(eventEntity, new LocalDate(2026, 2, 1), new LocalDate(2026, 2, 28));
        var march = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));
        var may = Expand(eventEntity, new LocalDate(2026, 5, 1), new LocalDate(2026, 5, 31));

        Assert.Multiple(() =>
        {
            Assert.That(february, Is.Empty);
            Assert.That(march.Single().Date, Is.EqualTo(new LocalDate(2026, 3, 15)));
            Assert.That(may.Single().Date, Is.EqualTo(new LocalDate(2026, 5, 15)));
        });
    }

    [Test]
    public void Expand_YearlyIntervalFromLeapDay_ClampsInCommonYears()
    {
        var eventEntity = Interval(1, RecurrenceUnit.Years, new DateOnly(2024, 2, 29));

        var occurrences = Expand(eventEntity, new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 28));

        Assert.That(occurrences.Single().Date, Is.EqualTo(new LocalDate(2027, 2, 28)));
    }

    [Test]
    public void Expand_WindowBeforeStartDate_ReturnsNothing()
    {
        var eventEntity = Interval(1, RecurrenceUnit.Days, new DateOnly(2026, 5, 1));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(occurrences, Is.Empty);
    }

    [Test]
    public void Expand_WindowAfterRepeatUntil_ReturnsNothing()
    {
        var eventEntity = Interval(1, RecurrenceUnit.Days, new DateOnly(2026, 1, 1), repeatUntil: new DateOnly(2026, 2, 1));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(occurrences, Is.Empty);
    }

    [Test]
    public void Expand_WeeklyWithLookahead_IncludesNextDayOnlyBeforeCutoff()
    {
        // 2026-03-03 — вторник, следующий день после однодневного окна 2026-03-02.
        var early = Weekly(0b0000010, WeeklyStart, new TimeSpan(6, 30, 0));
        var late = Weekly(0b0000010, WeeklyStart, new TimeSpan(12, 0, 0));

        var cutoff = new LocalTime(9, 0);
        var day = new LocalDate(2026, 3, 2);

        Assert.Multiple(() =>
        {
            Assert.That(
                RecurrenceExpander.Expand(early, day, day, TimeZone, cutoff).Single().Date,
                Is.EqualTo(new LocalDate(2026, 3, 3)));
            Assert.That(RecurrenceExpander.Expand(late, day, day, TimeZone, cutoff), Is.Empty);
        });
    }

    [Test]
    public void Expand_DailyIntervalOverMonth_StaysWithinCap()
    {
        var eventEntity = Interval(1, RecurrenceUnit.Days, new DateOnly(2026, 1, 1));

        var occurrences = Expand(eventEntity, new LocalDate(2026, 3, 1), new LocalDate(2026, 3, 31));

        Assert.That(occurrences, Has.Count.EqualTo(31));
        Assert.That(occurrences, Has.Count.LessThanOrEqualTo(RecurrenceExpander.MaxOccurrencesPerWindow));
    }

    [Test]
    public void Expand_YearlyOnLeapDayInCommonYear_ClampsInsteadOfThrowing()
    {
        var eventEntity = Event.CreateYearly("Годовщина", 2, 29, null, true, null, null, null, CreatedAtUtc);

        var occurrences = Expand(eventEntity, new LocalDate(2027, 2, 1), new LocalDate(2027, 2, 28));

        Assert.That(occurrences.Single().Date, Is.EqualTo(new LocalDate(2027, 2, 28)));
    }

    [Test]
    public void Expand_YearlyOnImpossibleDay_ClampsToLastDayOfMonth()
    {
        var eventEntity = Event.CreateYearly("Отчёт", 11, 31, null, true, null, null, null, CreatedAtUtc);

        var occurrences = Expand(eventEntity, new LocalDate(2026, 11, 1), new LocalDate(2026, 11, 30));

        Assert.That(occurrences.Single().Date, Is.EqualTo(new LocalDate(2026, 11, 30)));
    }

    private static IReadOnlyList<EventOccurrence> Expand(Event eventEntity, LocalDate start, LocalDate end)
        => RecurrenceExpander.Expand(eventEntity, start, end, TimeZone);

    private static Event Weekly(
        int mask,
        DateOnly startDate,
        TimeSpan? timeOfDay,
        bool isAllDay = false,
        DateOnly? repeatUntil = null)
        => Event.CreateWeekly(
            "Тренировка", mask, startDate, timeOfDay, isAllDay, repeatUntil, null, null, null, CreatedAtUtc);

    private static Event Interval(
        int count,
        RecurrenceUnit unit,
        DateOnly startDate,
        DateOnly? repeatUntil = null)
        => Event.CreateInterval(
            "Оплата счёта", count, unit, startDate, null, true, repeatUntil, null, null, null, CreatedAtUtc);
}
