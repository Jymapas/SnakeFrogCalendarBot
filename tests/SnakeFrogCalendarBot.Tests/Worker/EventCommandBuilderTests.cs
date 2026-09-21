using NodaTime;
using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Worker.Telegram;

namespace SnakeFrogCalendarBot.Tests.Worker;

public sealed class EventCommandBuilderTests
{
    private static readonly DateTimeZone TimeZone = DateTimeZoneProviders.Tzdb["Europe/Moscow"];

    private const int MondayAndWednesday = 0b0000101;

    [Test]
    public void TryBuild_WeeklyData_ProducesWeeklyCommand()
    {
        var data = new EventConversationData
        {
            Title = "Тренировка",
            Kind = "Weekly",
            DaysOfWeekMask = MondayAndWednesday,
            StartDate = new DateOnly(2026, 3, 2),
            IsAllDay = false,
            Hour = 19,
            Minute = 0
        };

        var failure = EventCommandBuilder.TryBuild(data, TimeZone, out var command);

        Assert.That(failure, Is.EqualTo(EventCommandBuildFailure.None));
        Assert.Multiple(() =>
        {
            Assert.That(command!.Kind, Is.EqualTo(EventKind.Weekly));
            Assert.That(command.DaysOfWeekMask, Is.EqualTo(MondayAndWednesday));
            Assert.That(command.StartDateLocal, Is.EqualTo(new DateOnly(2026, 3, 2)));
            Assert.That(command.TimeOfDay, Is.EqualTo(new TimeSpan(19, 0, 0)));
            Assert.That(command.RepeatUntilLocal, Is.Null);
        });
    }

    [Test]
    public void TryBuild_IntervalData_ProducesIntervalCommand()
    {
        var data = new EventConversationData
        {
            Title = "Оплата счёта",
            Kind = "Interval",
            IntervalCount = 2,
            IntervalUnit = "Months",
            StartDate = new DateOnly(2026, 3, 2),
            RepeatUntil = new DateOnly(2026, 12, 31),
            IsAllDay = true
        };

        var failure = EventCommandBuilder.TryBuild(data, TimeZone, out var command);

        Assert.That(failure, Is.EqualTo(EventCommandBuildFailure.None));
        Assert.Multiple(() =>
        {
            Assert.That(command!.Kind, Is.EqualTo(EventKind.Interval));
            Assert.That(command.IntervalCount, Is.EqualTo(2));
            Assert.That(command.IntervalUnit, Is.EqualTo(RecurrenceUnit.Months));
            Assert.That(command.RepeatUntilLocal, Is.EqualTo(new DateOnly(2026, 12, 31)));
            Assert.That(command.TimeOfDay, Is.Null);
        });
    }

    [Test]
    public void TryBuild_WeeklyWithoutDays_ReportsMissingSchedule()
    {
        var data = new EventConversationData
        {
            Title = "Тренировка",
            Kind = "Weekly",
            DaysOfWeekMask = 0,
            StartDate = new DateOnly(2026, 3, 2),
            IsAllDay = true
        };

        Assert.That(
            EventCommandBuilder.TryBuild(data, TimeZone, out _),
            Is.EqualTo(EventCommandBuildFailure.MissingSchedule));
    }

    [Test]
    public void TryBuild_IntervalWithoutStartDate_ReportsMissingSchedule()
    {
        var data = new EventConversationData
        {
            Title = "Оплата счёта",
            Kind = "Interval",
            IntervalCount = 2,
            IntervalUnit = "Weeks",
            IsAllDay = true
        };

        Assert.That(
            EventCommandBuilder.TryBuild(data, TimeZone, out _),
            Is.EqualTo(EventCommandBuildFailure.MissingSchedule));
    }

    [Test]
    public void TryBuild_OneOffData_StillProducesOneOffCommand()
    {
        var data = new EventConversationData
        {
            Title = "Совещание",
            Kind = "OneOff",
            Year = 2026,
            Month = 3,
            Day = 2,
            Hour = 10,
            Minute = 0,
            IsAllDay = false
        };

        var failure = EventCommandBuilder.TryBuild(data, TimeZone, out var command);

        Assert.That(failure, Is.EqualTo(EventCommandBuildFailure.None));
        Assert.That(command!.Kind, Is.EqualTo(EventKind.OneOff));
        Assert.That(
            command.OccursAtUtc,
            Is.EqualTo(new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.FromHours(3))));
    }
}
