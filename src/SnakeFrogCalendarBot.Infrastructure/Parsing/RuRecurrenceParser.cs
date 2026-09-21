using System.Text.RegularExpressions;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Infrastructure.Parsing;

/// <summary>
/// Распознаёт русские формулировки повторения: «каждый вторник 19:00», «по пн, ср, пт»,
/// «каждые 2 недели», «раз в 3 дня», «каждый день 08:30».
/// </summary>
public sealed partial class RuRecurrenceParser : IRecurrenceParser
{
    /// <summary>
    /// Основы названий дней недели; индекс совпадает с номером бита в маске (0 — понедельник).
    /// «понедельник» намеренно проверяется раньше «недели», иначе он съедается единицей интервала.
    /// </summary>
    private static readonly string[][] WeekdayStems =
    [
        ["понедельник", "пн"],
        ["вторник", "вт"],
        ["сред", "ср"],
        ["четверг", "чт"],
        ["пятниц", "пт"],
        ["суббот", "сб"],
        ["воскресень", "вс"]
    ];

    public bool TryParse(string input, out RecurrenceParseResult? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim().ToLowerInvariant().Replace('ё', 'е');

        var (hour, minute, withoutTime) = ExtractTime(normalized);

        var weekdayMask = ExtractWeekdayMask(withoutTime);
        if (weekdayMask != 0)
        {
            result = new RecurrenceParseResult(EventKind.Weekly, weekdayMask, null, null, hour, minute);
            return true;
        }

        var interval = ExtractInterval(withoutTime);
        if (interval is null)
        {
            return false;
        }

        result = new RecurrenceParseResult(
            EventKind.Interval,
            null,
            interval.Value.Count,
            interval.Value.Unit,
            hour,
            minute);

        return true;
    }

    private static (int? Hour, int? Minute, string WithoutTime) ExtractTime(string input)
    {
        var match = TimeRegex().Match(input);
        if (!match.Success)
        {
            return (null, null, input);
        }

        var hour = int.Parse(match.Groups[1].Value);
        var minute = int.Parse(match.Groups[2].Value);

        if (hour > 23 || minute > 59)
        {
            return (null, null, input);
        }

        return (hour, minute, input.Remove(match.Index, match.Length));
    }

    private static int ExtractWeekdayMask(string input)
    {
        // Без «каждый»/«по»/«еженедельно» название дня — это разовая дата, а не расписание.
        if (!RepeatMarkerRegex().IsMatch(input))
        {
            return 0;
        }

        var mask = 0;
        for (var bit = 0; bit < WeekdayStems.Length; bit++)
        {
            foreach (var stem in WeekdayStems[bit])
            {
                if (Regex.IsMatch(input, $@"\b{stem}"))
                {
                    mask |= 1 << bit;
                    break;
                }
            }
        }

        return mask;
    }

    private static (int Count, RecurrenceUnit Unit)? ExtractInterval(string input)
    {
        var match = IntervalRegex().Match(input);
        if (!match.Success)
        {
            return null;
        }

        var countGroup = match.Groups[1].Value;
        var count = string.IsNullOrEmpty(countGroup) ? 1 : int.Parse(countGroup);
        if (count < 1)
        {
            return null;
        }

        var unitToken = match.Groups[2].Value;
        var unit = unitToken switch
        {
            var t when t.StartsWith("недел", StringComparison.Ordinal) => RecurrenceUnit.Weeks,
            var t when t.StartsWith("месяц", StringComparison.Ordinal) => RecurrenceUnit.Months,
            var t when t.StartsWith("год", StringComparison.Ordinal) || t.StartsWith("лет", StringComparison.Ordinal)
                => RecurrenceUnit.Years,
            _ => RecurrenceUnit.Days
        };

        return (count, unit);
    }

    [GeneratedRegex(@"\b(\d{1,2}):(\d{2})\b")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\b(?:кажд\w*|по|еженедельно)\b")]
    private static partial Regex RepeatMarkerRegex();

    [GeneratedRegex(@"\b(?:кажд\w*|раз\s+в)\s+(\d+)?\s*(дн\w*|ден\w*|недел\w*|месяц\w*|год\w*|лет)\b")]
    private static partial Regex IntervalRegex();
}
