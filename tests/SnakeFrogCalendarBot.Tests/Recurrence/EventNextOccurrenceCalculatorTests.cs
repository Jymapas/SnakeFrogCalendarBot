using NodaTime;
using SnakeFrogCalendarBot.Application.Recurrence;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.Recurrence;

public sealed class EventNextOccurrenceCalculatorTests
{
    private static readonly DateTimeZone TimeZone = DateTimeZoneProviders.Tzdb["Europe/Moscow"];
    private static readonly DateTime CreatedAtUtc = new(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);

    // 2026-03-02 — понедельник.
    private static readonly LocalDate Today = new(2026, 3, 2);

    private const int Monday = 0b0000001;
    private const int Wednesday = 0b0000100;

    [Test]
    public void TryGetNext_OneOff_ReturnsItsOwnInstant()
    {
        var occursAt = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.FromHours(3));
        var eventEntity = Event.CreateOneOff("Совещание", occursAt, false, null, null, null, CreatedAtUtc);

        var next = Next(eventEntity);

        Assert.That(next, Is.EqualTo(Instant.FromDateTimeOffset(occursAt)));
    }

    [Test]
    public void TryGetNext_OneOffInThePast_ReturnsNull()
    {
        var eventEntity = Event.CreateOneOff(
            "Совещание",
            new DateTimeOffset(2026, 1, 5, 10, 0, 0, TimeSpan.FromHours(3)),
            false, null, null, null, CreatedAtUtc);

        Assert.That(Next(eventEntity), Is.Null);
    }

    [Test]
    public void TryGetNext_YearlyAlreadyPassedThisYear_RollsOverToNextYear()
    {
        var eventEntity = Event.CreateYearly("Годовщина", 1, 15, null, true, null, null, null, CreatedAtUtc);

        var next = Next(eventEntity);

        Assert.That(ToLocalDate(next), Is.EqualTo(new LocalDate(2027, 1, 15)));
    }

    [Test]
    public void TryGetNext_WeeklyOnMatchingDayToday_ReturnsToday()
    {
        var eventEntity = Weekly(Monday, new DateOnly(2026, 1, 5), new TimeSpan(19, 0, 0));

        var next = Next(eventEntity);

        Assert.That(ToLocalDate(next), Is.EqualTo(Today));
        Assert.That(ToLocalTime(next), Is.EqualTo(new LocalTime(19, 0)));
    }

    [Test]
    public void TryGetNext_WeeklyOnLaterDay_ReturnsNearestMatchingDay()
    {
        var eventEntity = Weekly(Wednesday, new DateOnly(2026, 1, 5), null, isAllDay: true);

        Assert.That(ToLocalDate(Next(eventEntity)), Is.EqualTo(new LocalDate(2026, 3, 4)));
    }

    [Test]
    public void TryGetNext_WeeklyStartingLater_ReturnsFirstDayAfterStart()
    {
        var eventEntity = Weekly(Monday, new DateOnly(2026, 4, 1), null, isAllDay: true);

        Assert.That(ToLocalDate(Next(eventEntity)), Is.EqualTo(new LocalDate(2026, 4, 6)));
    }

    [Test]
    public void TryGetNext_WeeklyPastRepeatUntil_ReturnsNull()
    {
        var eventEntity = Weekly(Monday, new DateOnly(2026, 1, 5), null, isAllDay: true, repeatUntil: new DateOnly(2026, 2, 28));

        Assert.That(Next(eventEntity), Is.Null);
    }

    [Test]
    public void TryGetNext_IntervalInDays_ReturnsNearestStep()
    {
        var eventEntity = Interval(3, RecurrenceUnit.Days, new DateOnly(2026, 3, 1));

        Assert.That(ToLocalDate(Next(eventEntity)), Is.EqualTo(new LocalDate(2026, 3, 4)));
    }

    [Test]
    public void TryGetNext_MonthlyIntervalFromEndOfMonth_KeepsAnchorDay()
    {
        var eventEntity = Interval(1, RecurrenceUnit.Months, new DateOnly(2026, 1, 31));

        Assert.That(ToLocalDate(Next(eventEntity)), Is.EqualTo(new LocalDate(2026, 3, 31)));
    }

    [Test]
    public void TryGetNext_YearlyInterval_ReturnsNextYearStep()
    {
        var eventEntity = Interval(2, RecurrenceUnit.Years, new DateOnly(2025, 6, 10));

        Assert.That(ToLocalDate(Next(eventEntity)), Is.EqualTo(new LocalDate(2027, 6, 10)));
    }

    private static Instant? Next(Event eventEntity)
        => EventNextOccurrenceCalculator.TryGetNextOccurrence(eventEntity, Today, TimeZone);

    private static LocalDate ToLocalDate(Instant? instant) => instant!.Value.InZone(TimeZone).Date;

    private static LocalTime ToLocalTime(Instant? instant) => instant!.Value.InZone(TimeZone).TimeOfDay;

    private static Event Weekly(
        int mask,
        DateOnly startDate,
        TimeSpan? timeOfDay,
        bool isAllDay = false,
        DateOnly? repeatUntil = null)
        => Event.CreateWeekly(
            "Тренировка", mask, startDate, timeOfDay, isAllDay, repeatUntil, null, null, null, CreatedAtUtc);

    private static Event Interval(int count, RecurrenceUnit unit, DateOnly startDate)
        => Event.CreateInterval(
            "Оплата счёта", count, unit, startDate, null, true, null, null, null, null, CreatedAtUtc);
}
