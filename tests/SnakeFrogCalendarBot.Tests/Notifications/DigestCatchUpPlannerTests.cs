using NodaTime;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class DigestCatchUpPlannerTests
{
    private readonly DigestCatchUpPlanner _planner = new();

    [Test]
    public void GetDueDigests_AfterDailyTrigger_ReturnsDaily()
    {
        var nowLocal = new LocalDateTime(2026, 2, 28, 10, 30);

        var dueDigests = _planner.GetDueDigests(nowLocal, TimeSpan.FromHours(3));

        Assert.That(dueDigests.Select(x => x.DigestType), Is.EquivalentTo(new[] { DigestType.Daily }));
        Assert.That(dueDigests.Single().TriggerDate, Is.EqualTo(new LocalDate(2026, 2, 28)));
    }

    [Test]
    public void GetDueDigests_AfterSundayWeeklyTriggerAcrossMidnight_ReturnsWeeklyForPreviousSunday()
    {
        var nowLocal = new LocalDateTime(2026, 3, 2, 0, 30);

        var dueDigests = _planner.GetDueDigests(nowLocal, TimeSpan.FromHours(4));

        var weekly = dueDigests.Single(x => x.DigestType == DigestType.Weekly);
        Assert.That(weekly.TriggerDate, Is.EqualTo(new LocalDate(2026, 3, 1)));
        Assert.That(weekly.ScheduledAtLocal, Is.EqualTo(new LocalDateTime(2026, 3, 1, 21, 0)));
    }

    [Test]
    public void GetDueDigests_AfterMonthBoundaryWithinWindow_ReturnsMonthlyForPreviousMonthEnd()
    {
        var nowLocal = new LocalDateTime(2026, 3, 1, 0, 30);

        var dueDigests = _planner.GetDueDigests(nowLocal, TimeSpan.FromHours(7));

        var monthly = dueDigests.Single(x => x.DigestType == DigestType.Monthly);
        Assert.That(monthly.TriggerDate, Is.EqualTo(new LocalDate(2026, 2, 28)));
        Assert.That(monthly.ScheduledAtLocal, Is.EqualTo(new LocalDateTime(2026, 2, 28, 18, 0)));
    }

    [Test]
    public void GetDueDigests_OutsideWindow_ReturnsEmpty()
    {
        var nowLocal = new LocalDateTime(2026, 2, 28, 13, 1);

        var dueDigests = _planner.GetDueDigests(nowLocal, TimeSpan.FromHours(3));

        Assert.That(dueDigests, Is.Empty);
    }
}
