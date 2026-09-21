using NodaTime;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Recurrence;

/// <summary>
/// Раскрывает событие в конкретные вхождения внутри окна [periodStart, periodEnd].
/// Разовые и ежегодные события дают не больше одного вхождения, повторяющиеся — несколько.
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>
    /// Предел вхождений на одно событие в одном окне: страховка от некорректных данных.
    /// </summary>
    public const int MaxOccurrencesPerWindow = 64;

    public static IReadOnlyList<EventOccurrence> Expand(
        Event eventEntity,
        LocalDate periodStart,
        LocalDate periodEnd,
        DateTimeZone timeZone,
        LocalTime? lookaheadCutoff = null)
    {
        var searchEnd = lookaheadCutoff.HasValue ? periodEnd.PlusDays(1) : periodEnd;
        if (searchEnd < periodStart)
        {
            return [];
        }

        var occurrences = ExpandCore(eventEntity, periodStart, searchEnd, timeZone);
        if (occurrences.Count == 0 || !lookaheadCutoff.HasValue)
        {
            return occurrences;
        }

        // День заглядывания: из следующего дня попадают только события со временем раньше cutoff.
        return occurrences
            .Where(occurrence => occurrence.Date <= periodEnd
                || (occurrence.Time.HasValue && occurrence.Time.Value < lookaheadCutoff.Value))
            .ToList();
    }

    public static bool TryResolve(
        Birthday birthday,
        LocalDate periodStart,
        LocalDate periodEnd,
        out LocalDate date)
    {
        var targetDate = ResolveYearlyDate(periodStart, periodEnd, birthday.Month, birthday.Day);
        if (!targetDate.HasValue)
        {
            date = default;
            return false;
        }

        date = targetDate.Value;
        return true;
    }

    private static IReadOnlyList<EventOccurrence> ExpandCore(
        Event eventEntity,
        LocalDate windowStart,
        LocalDate windowEnd,
        DateTimeZone timeZone)
    {
        switch (eventEntity.Kind)
        {
            case EventKind.OneOff:
                return ExpandOneOff(eventEntity, windowStart, windowEnd, timeZone);
            case EventKind.Yearly:
                return ExpandYearly(eventEntity, windowStart, windowEnd);
            case EventKind.Weekly:
                return ExpandWeekly(eventEntity, windowStart, windowEnd);
            case EventKind.Interval:
                return ExpandInterval(eventEntity, windowStart, windowEnd);
            default:
                return [];
        }
    }

    private static IReadOnlyList<EventOccurrence> ExpandOneOff(
        Event eventEntity,
        LocalDate windowStart,
        LocalDate windowEnd,
        DateTimeZone timeZone)
    {
        if (!eventEntity.OccursAtUtc.HasValue)
        {
            return [];
        }

        var occurrence = Instant.FromDateTimeOffset(eventEntity.OccursAtUtc.Value).InZone(timeZone).LocalDateTime;
        if (occurrence.Date < windowStart || occurrence.Date > windowEnd)
        {
            return [];
        }

        var time = eventEntity.IsAllDay ? (LocalTime?)null : occurrence.TimeOfDay;
        return [new EventOccurrence(occurrence.Date, time)];
    }

    private static IReadOnlyList<EventOccurrence> ExpandYearly(
        Event eventEntity,
        LocalDate windowStart,
        LocalDate windowEnd)
    {
        if (!eventEntity.Month.HasValue || !eventEntity.Day.HasValue)
        {
            return [];
        }

        var targetDate = ResolveYearlyDate(windowStart, windowEnd, eventEntity.Month.Value, eventEntity.Day.Value);
        if (!targetDate.HasValue)
        {
            return [];
        }

        return [new EventOccurrence(targetDate.Value, ResolveTimeOfDay(eventEntity))];
    }

    private static IReadOnlyList<EventOccurrence> ExpandWeekly(
        Event eventEntity,
        LocalDate windowStart,
        LocalDate windowEnd)
    {
        if (!eventEntity.DaysOfWeekMask.HasValue || !TryNarrowWindow(eventEntity, ref windowStart, ref windowEnd))
        {
            return [];
        }

        var mask = eventEntity.DaysOfWeekMask.Value;
        var time = ResolveTimeOfDay(eventEntity);
        var occurrences = new List<EventOccurrence>();

        for (var date = windowStart; date <= windowEnd; date = date.PlusDays(1))
        {
            var bit = 1 << ((int)date.DayOfWeek - 1);
            if ((mask & bit) != 0)
            {
                occurrences.Add(new EventOccurrence(date, time));
            }
        }

        return occurrences;
    }

    private static IReadOnlyList<EventOccurrence> ExpandInterval(
        Event eventEntity,
        LocalDate windowStart,
        LocalDate windowEnd)
    {
        if (!eventEntity.IntervalCount.HasValue
            || !eventEntity.IntervalUnit.HasValue
            || eventEntity.IntervalCount.Value < 1
            || !eventEntity.StartDateLocal.HasValue)
        {
            return [];
        }

        var anchor = ToLocalDate(eventEntity.StartDateLocal.Value);
        if (!TryNarrowWindow(eventEntity, ref windowStart, ref windowEnd))
        {
            return [];
        }

        var time = ResolveTimeOfDay(eventEntity);
        var count = eventEntity.IntervalCount.Value;

        return eventEntity.IntervalUnit.Value switch
        {
            RecurrenceUnit.Days => ExpandByDays(anchor, count, windowStart, windowEnd, time),
            RecurrenceUnit.Weeks => ExpandByDays(anchor, count * 7, windowStart, windowEnd, time),
            RecurrenceUnit.Months => ExpandByMonths(anchor, count, windowStart, windowEnd, time),
            RecurrenceUnit.Years => ExpandByMonths(anchor, count * 12, windowStart, windowEnd, time),
            _ => []
        };
    }

    private static IReadOnlyList<EventOccurrence> ExpandByDays(
        LocalDate anchor,
        int stepDays,
        LocalDate windowStart,
        LocalDate windowEnd,
        LocalTime? time)
    {
        var delta = Period.Between(anchor, windowStart, PeriodUnits.Days).Days;
        var firstStep = delta <= 0 ? 0 : (int)((delta + stepDays - 1) / stepDays);

        var occurrences = new List<EventOccurrence>();
        var date = anchor.PlusDays(firstStep * stepDays);

        while (date <= windowEnd && occurrences.Count < MaxOccurrencesPerWindow)
        {
            if (date >= windowStart)
            {
                occurrences.Add(new EventOccurrence(date, time));
            }

            date = date.PlusDays(stepDays);
        }

        return occurrences;
    }

    /// <summary>
    /// Каждое вхождение считается от anchor, а не от предыдущего вхождения: NodaTime клампит
    /// день к последнему валидному в месяце, поэтому PlusMonths не композируется — 31 января
    /// плюс месяц дважды даёт 28 марта вместо 31-го.
    /// </summary>
    private static IReadOnlyList<EventOccurrence> ExpandByMonths(
        LocalDate anchor,
        int stepMonths,
        LocalDate windowStart,
        LocalDate windowEnd,
        LocalTime? time)
    {
        var monthsDelta = ((windowStart.Year - anchor.Year) * 12) + (windowStart.Month - anchor.Month);
        var step = monthsDelta <= 0 ? 0 : Math.Max(0, (monthsDelta / stepMonths) - 1);

        var occurrences = new List<EventOccurrence>();

        for (var guard = 0; guard < MaxOccurrencesPerWindow; guard++, step++)
        {
            var candidate = anchor.PlusMonths(step * stepMonths);
            if (candidate > windowEnd)
            {
                break;
            }

            if (candidate >= windowStart)
            {
                occurrences.Add(new EventOccurrence(candidate, time));
            }
        }

        return occurrences;
    }

    /// <summary>
    /// Сжимает окно до пересечения с периодом жизни расписания [StartDateLocal, RepeatUntilLocal].
    /// </summary>
    private static bool TryNarrowWindow(Event eventEntity, ref LocalDate windowStart, ref LocalDate windowEnd)
    {
        if (!eventEntity.StartDateLocal.HasValue)
        {
            return false;
        }

        var startDate = ToLocalDate(eventEntity.StartDateLocal.Value);
        if (startDate > windowStart)
        {
            windowStart = startDate;
        }

        if (eventEntity.RepeatUntilLocal.HasValue)
        {
            var repeatUntil = ToLocalDate(eventEntity.RepeatUntilLocal.Value);
            if (repeatUntil < windowEnd)
            {
                windowEnd = repeatUntil;
            }
        }

        return windowStart <= windowEnd;
    }

    private static LocalDate ToLocalDate(DateOnly value) => new(value.Year, value.Month, value.Day);

    private static LocalTime? ResolveTimeOfDay(Event eventEntity)
    {
        if (eventEntity.IsAllDay || !eventEntity.TimeOfDay.HasValue)
        {
            return null;
        }

        return LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks);
    }

    private static LocalDate? ResolveYearlyDate(LocalDate windowStart, LocalDate windowEnd, int month, int day)
    {
        var candidate = new LocalDate(windowStart.Year, month, day);
        if (candidate < windowStart)
        {
            candidate = candidate.PlusYears(1);
        }

        return candidate >= windowStart && candidate <= windowEnd
            ? candidate
            : null;
    }
}
