namespace SnakeFrogCalendarBot.Application.Abstractions.Parsing;

public interface IBirthdayDateParser
{
    bool TryParseMonthDay(string input, out int day, out int month);
}
