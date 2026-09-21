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
