using NodaTime;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Recurrence;

/// <summary>
/// Находит ближайшее вхождение события начиная с указанной локальной даты.
/// Нужен спискам событий и форматтерам, где у события одна «дата», а не период.
/// </summary>
public static class EventNextOccurrenceCalculator
{
    public static Instant? TryGetNextOccurrence(Event eventEntity, LocalDate fromDate, DateTimeZone timeZone)
    {
        var occurrence = TryGetNextLocalOccurrence(eventEntity, fromDate, timeZone);
        if (!occurrence.HasValue)
        {
            return null;
        }

        var localDateTime = occurrence.Value.Time.HasValue
            ? occurrence.Value.Date.At(occurrence.Value.Time.Value)
            : occurrence.Value.Date.AtMidnight();

        return localDateTime.InZoneLeniently(timeZone).ToInstant();
    }

    public static EventOccurrence? TryGetNextLocalOccurrence(
        Event eventEntity,
        LocalDate fromDate,
        DateTimeZone timeZone)
    {
        var searchStart = fromDate;
        if (eventEntity.StartDateLocal.HasValue)
        {
            var startDate = ToLocalDate(eventEntity.StartDateLocal.Value);
            if (startDate > searchStart)
            {
                searchStart = startDate;
            }
        }

        var searchEnd = ResolveSearchEnd(eventEntity, searchStart);
        if (searchEnd < searchStart)
        {
            return null;
        }

        var occurrences = RecurrenceExpander.Expand(eventEntity, searchStart, searchEnd, timeZone);
        return occurrences.Count > 0 ? occurrences[0] : null;
    }

    /// <summary>
    /// Верхняя граница поиска: окно, в которое гарантированно попадает хотя бы один шаг расписания.
    /// </summary>
    private static LocalDate ResolveSearchEnd(Event eventEntity, LocalDate searchStart)
    {
        switch (eventEntity.Kind)
        {
            case EventKind.OneOff:
                // Разовое событие раскрывается по собственной дате, окно должно её накрыть.
                return LocalDate.MaxIsoValue;
            case EventKind.Yearly:
                return searchStart.PlusYears(1);
            case EventKind.Weekly:
                return searchStart.PlusDays(6);
            case EventKind.Interval:
                return ResolveIntervalSearchEnd(eventEntity, searchStart);
            default:
                return searchStart;
        }
    }

    private static LocalDate ResolveIntervalSearchEnd(Event eventEntity, LocalDate searchStart)
    {
        if (!eventEntity.IntervalCount.HasValue || !eventEntity.IntervalUnit.HasValue)
        {
            return searchStart;
        }

        var count = eventEntity.IntervalCount.Value;

        return eventEntity.IntervalUnit.Value switch
        {
            RecurrenceUnit.Days => searchStart.PlusDays(count),
            RecurrenceUnit.Weeks => searchStart.PlusDays(count * 7),
            // Запас в месяц покрывает клампинг конца месяца (31 января → 28 февраля).
            RecurrenceUnit.Months => searchStart.PlusMonths(count + 1),
            RecurrenceUnit.Years => searchStart.PlusYears(count).PlusMonths(1),
            _ => searchStart
        };
    }

    private static LocalDate ToLocalDate(DateOnly value) => new(value.Year, value.Month, value.Day);
}
