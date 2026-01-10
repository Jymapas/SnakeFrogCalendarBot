using System.Globalization;
using System.Text;
using NodaTime;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Formatting;

public sealed class DigestFormatter
{
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");

    public string FormatDaily(LocalDate date, IReadOnlyList<CalendarItemDto> items)
    {
        var builder = new StringBuilder();
        builder.Append("📅 Сегодня (");
        builder.Append(date.ToString("d MMMM", _culture));
        builder.AppendLine(")");

        if (items.Count == 0)
        {
            builder.Append("Сегодня событий и дней рождения нет");
            return builder.ToString();
        }

        var groupedByTime = items
            .GroupBy(i => i.Time ?? LocalTime.MaxValue)
            .OrderBy(g => g.Key);

        foreach (var timeGroup in groupedByTime)
        {
            var time = timeGroup.Key;
            var timeItems = timeGroup.OrderBy(i => i.Type).ThenBy(i => i.Title);

            foreach (var item in timeItems)
            {
                if (item.Type == CalendarItemType.Birthday)
                {
                    builder.Append("🎂 ");
                    builder.Append(item.Title);
                    if (item.BirthYear.HasValue)
                    {
                        builder.Append(" (");
                        builder.Append(item.BirthYear.Value);
                        builder.Append(")");
                    }
                }
                else
                {
                    builder.Append("📅 ");
                    if (!item.IsAllDay && time != LocalTime.MaxValue)
                    {
                        builder.Append(time.ToString("HH:mm", CultureInfo.InvariantCulture));
                        builder.Append(" — ");
                    }
                    builder.Append(item.Title);
                    if (item.HasAttachment)
                    {
                        builder.Append(" 📎");
                    }
                }
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    public string FormatWeekly(LocalDate periodStart, LocalDate periodEnd, IReadOnlyList<CalendarItemDto> items)
    {
        var builder = new StringBuilder();
        builder.Append("📆 События на неделю (");
        builder.Append(periodStart.ToString("d MMMM", _culture));
        builder.Append("–");
        builder.Append(periodEnd.ToString("d MMMM", _culture));
        builder.AppendLine(")");
        builder.AppendLine();

        if (items.Count == 0)
        {
            builder.Append("На эту неделю событий и дней рождения нет");
            return builder.ToString();
        }

        var groupedByDate = items.GroupBy(i => i.Date).OrderBy(g => g.Key);

        foreach (var dateGroup in groupedByDate)
        {
            var date = dateGroup.Key;
            var dayName = GetDayName(date);
            builder.Append(dayName);
            builder.Append(", ");
            builder.Append(date.ToString("d MMMM", _culture));
            builder.AppendLine();

            var dateItems = dateGroup
                .OrderBy(i => i.Time ?? LocalTime.MaxValue)
                .ThenBy(i => i.Type)
                .ThenBy(i => i.Title);

            foreach (var item in dateItems)
            {
                if (item.Type == CalendarItemType.Birthday)
                {
                    builder.Append("🎂 ");
                    builder.Append(item.Title);
                    if (item.BirthYear.HasValue)
                    {
                        builder.Append(" (");
                        builder.Append(item.BirthYear.Value);
                        builder.Append(")");
                    }
                }
                else
                {
                    builder.Append("📅 ");
                    if (!item.IsAllDay && item.Time.HasValue)
                    {
                        builder.Append(item.Time.Value.ToString("HH:mm", CultureInfo.InvariantCulture));
                        builder.Append(" — ");
                    }
                    builder.Append(item.Title);
                    if (item.HasAttachment)
                    {
                        builder.Append(" 📎");
                    }
                }
                builder.AppendLine();
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public string FormatMonthly(LocalDate periodStart, LocalDate periodEnd, IReadOnlyList<CalendarItemDto> items)
    {
        var builder = new StringBuilder();
        builder.Append("📆 События на месяц (");
        builder.Append(periodStart.ToString("MMMM yyyy", _culture));
        builder.AppendLine(")");
        builder.AppendLine();

        if (items.Count == 0)
        {
            builder.Append("На этот месяц событий и дней рождения нет");
            return builder.ToString();
        }

        var groupedByDate = items.GroupBy(i => i.Date).OrderBy(g => g.Key);

        foreach (var dateGroup in groupedByDate)
        {
            var date = dateGroup.Key;
            builder.Append(date.ToString("d MMMM", _culture));
            builder.AppendLine();

            var dateItems = dateGroup
                .OrderBy(i => i.Time ?? LocalTime.MaxValue)
                .ThenBy(i => i.Type)
                .ThenBy(i => i.Title);

            foreach (var item in dateItems)
            {
                if (item.Type == CalendarItemType.Birthday)
                {
                    builder.Append("🎂 ");
                    builder.Append(item.Title);
                    if (item.BirthYear.HasValue)
                    {
                        builder.Append(" (");
                        builder.Append(item.BirthYear.Value);
                        builder.Append(")");
                    }
                }
                else
                {
                    builder.Append("📅 ");
                    if (!item.IsAllDay && item.Time.HasValue)
                    {
                        builder.Append(item.Time.Value.ToString("HH:mm", CultureInfo.InvariantCulture));
                        builder.Append(" — ");
                    }
                    builder.Append(item.Title);
                    if (item.HasAttachment)
                    {
                        builder.Append(" 📎");
                    }
                }
                builder.AppendLine();
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private string GetDayName(LocalDate date)
    {
        var dayOfWeek = date.DayOfWeek;
        return dayOfWeek switch
        {
            IsoDayOfWeek.Monday => "Понедельник",
            IsoDayOfWeek.Tuesday => "Вторник",
            IsoDayOfWeek.Wednesday => "Среда",
            IsoDayOfWeek.Thursday => "Четверг",
            IsoDayOfWeek.Friday => "Пятница",
            IsoDayOfWeek.Saturday => "Суббота",
            IsoDayOfWeek.Sunday => "Воскресенье",
            _ => date.ToString("dddd", _culture)
        };
    }
}