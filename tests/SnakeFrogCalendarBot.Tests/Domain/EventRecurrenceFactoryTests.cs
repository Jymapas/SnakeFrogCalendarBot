using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Tests.Domain;

public sealed class EventRecurrenceFactoryTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly StartDate = new(2026, 3, 2);

    private const int MondayAndWednesday = 0b0000101;

    [Test]
    public void CreateWeekly_WithSeveralDays_StoresSchedule()
    {
        var eventEntity = Event.CreateWeekly(
            "Тренировка",
            MondayAndWednesday,
            StartDate,
            new TimeSpan(19, 0, 0),
            false,
            null,
            null,
            null,
            null,
            CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(eventEntity.Kind, Is.EqualTo(EventKind.Weekly));
            Assert.That(eventEntity.DaysOfWeekMask, Is.EqualTo(MondayAndWednesday));
            Assert.That(eventEntity.StartDateLocal, Is.EqualTo(StartDate));
            Assert.That(eventEntity.TimeOfDay, Is.EqualTo(new TimeSpan(19, 0, 0)));
            Assert.That(eventEntity.RepeatUntilLocal, Is.Null);
            Assert.That(eventEntity.IsRecurringSchedule, Is.True);
        });
    }

    [Test]
    public void CreateWeekly_WithEmptyMask_Throws()
    {
        Assert.Throws<DomainException>(() => Event.CreateWeekly(
            "Тренировка", 0, StartDate, null, true, null, null, null, null, CreatedAtUtc));
    }

    [Test]
    public void CreateWeekly_WithMaskBeyondSevenDays_Throws()
    {
        Assert.Throws<DomainException>(() => Event.CreateWeekly(
            "Тренировка", 0b10000000, StartDate, null, true, null, null, null, null, CreatedAtUtc));
    }

    [Test]
    public void CreateWeekly_WithRepeatUntilBeforeStart_Throws()
    {
        Assert.Throws<DomainException>(() => Event.CreateWeekly(
            "Тренировка",
            MondayAndWednesday,
            StartDate,
            null,
            true,
            StartDate.AddDays(-1),
            null,
            null,
            null,
            CreatedAtUtc));
    }

    [Test]
    public void CreateWeekly_WithTimeOnAllDayEvent_Throws()
    {
        Assert.Throws<DomainException>(() => Event.CreateWeekly(
            "Тренировка",
            MondayAndWednesday,
            StartDate,
            new TimeSpan(19, 0, 0),
            true,
            null,
            null,
            null,
            null,
            CreatedAtUtc));
    }

    [Test]
    public void CreateInterval_StoresSchedule()
    {
        var repeatUntil = new DateOnly(2026, 12, 31);

        var eventEntity = Event.CreateInterval(
            "Оплата счёта",
            2,
            RecurrenceUnit.Months,
            StartDate,
            null,
            true,
            repeatUntil,
            null,
            null,
            null,
            CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(eventEntity.Kind, Is.EqualTo(EventKind.Interval));
            Assert.That(eventEntity.IntervalCount, Is.EqualTo(2));
            Assert.That(eventEntity.IntervalUnit, Is.EqualTo(RecurrenceUnit.Months));
            Assert.That(eventEntity.StartDateLocal, Is.EqualTo(StartDate));
            Assert.That(eventEntity.RepeatUntilLocal, Is.EqualTo(repeatUntil));
            Assert.That(eventEntity.IsRecurringSchedule, Is.True);
        });
    }

    [Test]
    public void CreateInterval_WithNonPositiveCount_Throws()
    {
        Assert.Throws<DomainException>(() => Event.CreateInterval(
            "Оплата счёта", 0, RecurrenceUnit.Days, StartDate, null, true, null, null, null, null, CreatedAtUtc));
    }

    [Test]
    public void CreateInterval_WithUnknownUnit_Throws()
    {
        Assert.Throws<DomainException>(() => Event.CreateInterval(
            "Оплата счёта", 1, (RecurrenceUnit)42, StartDate, null, true, null, null, null, null, CreatedAtUtc));
    }

    [Test]
    public void UpdateWeeklySchedule_OnIntervalEvent_Throws()
    {
        var eventEntity = Event.CreateInterval(
            "Оплата счёта", 2, RecurrenceUnit.Months, StartDate, null, true, null, null, null, null, CreatedAtUtc);

        Assert.Throws<DomainException>(() => eventEntity.UpdateWeeklySchedule(
            MondayAndWednesday, StartDate, null, true, CreatedAtUtc));
    }

    [Test]
    public void UpdateIntervalSchedule_ReplacesSchedule()
    {
        var eventEntity = Event.CreateInterval(
            "Оплата счёта", 2, RecurrenceUnit.Months, StartDate, null, true, null, null, null, null, CreatedAtUtc);

        var newStart = new DateOnly(2026, 4, 1);
        eventEntity.UpdateIntervalSchedule(3, RecurrenceUnit.Weeks, newStart, new TimeSpan(8, 0, 0), false, CreatedAtUtc);

        Assert.Multiple(() =>
        {
            Assert.That(eventEntity.IntervalCount, Is.EqualTo(3));
            Assert.That(eventEntity.IntervalUnit, Is.EqualTo(RecurrenceUnit.Weeks));
            Assert.That(eventEntity.StartDateLocal, Is.EqualTo(newStart));
            Assert.That(eventEntity.TimeOfDay, Is.EqualTo(new TimeSpan(8, 0, 0)));
            Assert.That(eventEntity.IsAllDay, Is.False);
        });
    }

    [Test]
    public void UpdateRepeatUntil_ClearsAndSetsBoundary()
    {
        var eventEntity = Event.CreateWeekly(
            "Тренировка", MondayAndWednesday, StartDate, null, true, new DateOnly(2026, 6, 1),
            null, null, null, CreatedAtUtc);

        eventEntity.UpdateRepeatUntil(null, CreatedAtUtc);
        Assert.That(eventEntity.RepeatUntilLocal, Is.Null);

        eventEntity.UpdateRepeatUntil(new DateOnly(2026, 7, 1), CreatedAtUtc);
        Assert.That(eventEntity.RepeatUntilLocal, Is.EqualTo(new DateOnly(2026, 7, 1)));
    }

    [Test]
    public void UpdateRepeatUntil_BeforeStartDate_Throws()
    {
        var eventEntity = Event.CreateWeekly(
            "Тренировка", MondayAndWednesday, StartDate, null, true, null, null, null, null, CreatedAtUtc);

        Assert.Throws<DomainException>(() => eventEntity.UpdateRepeatUntil(StartDate.AddDays(-1), CreatedAtUtc));
    }

    [Test]
    public void UpdateRepeatUntil_OnOneOffEvent_Throws()
    {
        var eventEntity = Event.CreateOneOff(
            "Совещание",
            new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3)),
            false,
            null,
            null,
            null,
            CreatedAtUtc);

        Assert.Throws<DomainException>(() => eventEntity.UpdateRepeatUntil(new DateOnly(2026, 7, 1), CreatedAtUtc));
    }
}
