using NodaTime;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class DigestCatchUpPlanner
{
    private static readonly LocalTime DailyTriggerTime = new(9, 0);
    private static readonly LocalTime WeeklyTriggerTime = new(21, 0);
    private static readonly LocalTime MonthlyTriggerTime = new(18, 0);

    public IReadOnlyList<DueDigestSlot> GetDueDigests(LocalDateTime nowLocal, TimeSpan triggerWindow)
    {
        if (triggerWindow <= TimeSpan.Zero)
        {
            return [];
        }

        var dueSlots = new List<DueDigestSlot>();
        AddIfDue(dueSlots, DigestType.Daily, GetMostRecentDailySlot(nowLocal), nowLocal, triggerWindow);
        AddIfDue(dueSlots, DigestType.Weekly, GetMostRecentWeeklySlot(nowLocal), nowLocal, triggerWindow);
        AddIfDue(dueSlots, DigestType.Monthly, GetMostRecentMonthlySlot(nowLocal), nowLocal, triggerWindow);

        return dueSlots
            .OrderBy(slot => slot.ScheduledAtLocal)
            .ToList();
    }

    private static void AddIfDue(
        ICollection<DueDigestSlot> dueSlots,
        DigestType digestType,
        LocalDateTime scheduledAtLocal,
        LocalDateTime nowLocal,
        TimeSpan triggerWindow)
    {
        if (scheduledAtLocal > nowLocal)
        {
            return;
        }

        var elapsed = nowLocal.ToDateTimeUnspecified() - scheduledAtLocal.ToDateTimeUnspecified();
        if (elapsed > triggerWindow)
        {
            return;
        }

        dueSlots.Add(new DueDigestSlot(digestType, scheduledAtLocal.Date, scheduledAtLocal));
    }

    private static LocalDateTime GetMostRecentDailySlot(LocalDateTime nowLocal)
    {
        var scheduledAt = nowLocal.Date.At(DailyTriggerTime);
        return scheduledAt <= nowLocal
            ? scheduledAt
            : nowLocal.Date.PlusDays(-1).At(DailyTriggerTime);
    }

    private static LocalDateTime GetMostRecentWeeklySlot(LocalDateTime nowLocal)
    {
        var daysSinceSunday = nowLocal.Date.DayOfWeek == IsoDayOfWeek.Sunday
            ? 0
            : (int)nowLocal.Date.DayOfWeek;
        var scheduledDate = nowLocal.Date.PlusDays(-daysSinceSunday);
        var scheduledAt = scheduledDate.At(WeeklyTriggerTime);

        return scheduledAt <= nowLocal
            ? scheduledAt
            : scheduledDate.PlusDays(-7).At(WeeklyTriggerTime);
    }

    private static LocalDateTime GetMostRecentMonthlySlot(LocalDateTime nowLocal)
    {
        var currentMonthLastDay = CalendarSystem.Iso.GetDaysInMonth(nowLocal.Year, nowLocal.Month);
        var scheduledDate = new LocalDate(nowLocal.Year, nowLocal.Month, currentMonthLastDay);
        var scheduledAt = scheduledDate.At(MonthlyTriggerTime);
        if (scheduledAt <= nowLocal)
        {
            return scheduledAt;
        }

        var previousMonth = nowLocal.Date.PlusMonths(-1);
        var previousMonthLastDay = CalendarSystem.Iso.GetDaysInMonth(previousMonth.Year, previousMonth.Month);
        return new LocalDate(previousMonth.Year, previousMonth.Month, previousMonthLastDay).At(MonthlyTriggerTime);
    }
}

public sealed record DueDigestSlot(
    DigestType DigestType,
    LocalDate TriggerDate,
    LocalDateTime ScheduledAtLocal);
