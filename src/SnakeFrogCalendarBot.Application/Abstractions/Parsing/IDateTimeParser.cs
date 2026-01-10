namespace SnakeFrogCalendarBot.Application.Abstractions.Parsing;

public sealed record DateTimeParseResult(
    int Year,
    int Month,
    int Day,
    int? Hour,
    int? Minute,
    bool HasYear);

public interface IDateTimeParser
{
    bool TryParse(string input, out DateTimeParseResult? result);
}