using System.Globalization;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;

namespace SnakeFrogCalendarBot.Infrastructure.Parsing;

public sealed class RuBirthdayDateParser : IBirthdayDateParser
{
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly string[] Formats =
    {
        "d MMMM",
        "d MMMM yyyy",
        "d.MM",
        "dd.MM",
        "d.MM.yyyy",
        "dd.MM.yyyy",
        "yyyy-MM-dd"
    };

    public bool TryParseMonthDay(string input, out int day, out int month)
    {
        day = 0;
        month = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                input.Trim(),
                Formats,
                _culture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return false;
        }

        day = parsed.Day;
        month = parsed.Month;
        return true;
    }
}
