using System.Globalization;
using NodaTime;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Formatting;

/// <summary>
/// Человекочитаемое описание расписания события: «ежегодно», «еженедельно: пн, ср»,
/// «каждые 2 недели», «до 31 декабря».
/// </summary>
public static class RecurrenceDescriber
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly string[] ShortDayNames = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];

    /// <summary>
    /// Описание повторения без границы окончания. Для разовых событий — null.
    /// </summary>
    public static string? Describe(Event eventEntity)
    {
        return eventEntity.Kind switch
        {
            EventKind.Yearly => "ежегодно",
            EventKind.Weekly => DescribeWeekly(eventEntity),
            EventKind.Interval => DescribeInterval(eventEntity),
            _ => null
        };
    }

    /// <summary>
    /// Описание даты окончания повторения, либо null, если повторение бессрочное.
    /// </summary>
    public static string? DescribeBoundary(Event eventEntity)
    {
        if (!eventEntity.RepeatUntilLocal.HasValue)
        {
            return null;
        }

        var value = eventEntity.RepeatUntilLocal.Value;
        var date = new LocalDate(value.Year, value.Month, value.Day);
        return $"до {date.ToString("d MMMM yyyy", Culture)}";
    }

    /// <summary>
    /// Описание повторения вместе с границей окончания: «еженедельно: пн, ср, до 31 декабря 2026».
    /// </summary>
    public static string? DescribeFull(Event eventEntity)
    {
        var schedule = Describe(eventEntity);
        if (schedule is null)
        {
            return null;
        }

        var boundary = DescribeBoundary(eventEntity);
        return boundary is null ? schedule : $"{schedule}, {boundary}";
    }

    public static string DescribeDaysOfWeek(int daysOfWeekMask)
    {
        var names = new List<string>();
        for (var bit = 0; bit < ShortDayNames.Length; bit++)
        {
            if ((daysOfWeekMask & (1 << bit)) != 0)
            {
                names.Add(ShortDayNames[bit]);
            }
        }

        return string.Join(", ", names);
    }

    private static string DescribeWeekly(Event eventEntity)
    {
        if (!eventEntity.DaysOfWeekMask.HasValue)
        {
            return "еженедельно";
        }

        return $"еженедельно: {DescribeDaysOfWeek(eventEntity.DaysOfWeekMask.Value)}";
    }

    private static string DescribeInterval(Event eventEntity)
    {
        if (!eventEntity.IntervalCount.HasValue || !eventEntity.IntervalUnit.HasValue)
        {
            return "периодически";
        }

        var count = eventEntity.IntervalCount.Value;
        var unit = eventEntity.IntervalUnit.Value;

        if (count == 1)
        {
            return unit switch
            {
                RecurrenceUnit.Days => "каждый день",
                RecurrenceUnit.Weeks => "каждую неделю",
                RecurrenceUnit.Months => "каждый месяц",
                RecurrenceUnit.Years => "каждый год",
                _ => "периодически"
            };
        }

        var noun = unit switch
        {
            RecurrenceUnit.Days => Pluralize(count, "день", "дня", "дней"),
            RecurrenceUnit.Weeks => Pluralize(count, "неделю", "недели", "недель"),
            RecurrenceUnit.Months => Pluralize(count, "месяц", "месяца", "месяцев"),
            RecurrenceUnit.Years => Pluralize(count, "год", "года", "лет"),
            _ => string.Empty
        };

        return $"каждые {count} {noun}";
    }

    private static string Pluralize(int count, string one, string few, string many)
    {
        var lastTwo = count % 100;
        if (lastTwo is >= 11 and <= 14)
        {
            return many;
        }

        return (count % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }
}
