using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Abstractions.Parsing;

/// <summary>
/// Распознанное расписание повторения: либо Weekly с маской дней недели,
/// либо Interval с количеством и единицей. Время — опционально.
/// </summary>
public sealed record RecurrenceParseResult(
    EventKind Kind,
    int? DaysOfWeekMask,
    int? IntervalCount,
    RecurrenceUnit? IntervalUnit,
    int? Hour,
    int? Minute);

public interface IRecurrenceParser
{
    bool TryParse(string input, out RecurrenceParseResult? result);
}
