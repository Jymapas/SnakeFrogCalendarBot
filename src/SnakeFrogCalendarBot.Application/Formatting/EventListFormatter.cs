using System.Globalization;
using System.Text;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Formatting;

public sealed class EventListFormatter
{
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");

    public EventListFormatter(ITimeZoneProvider timeZoneProvider)
    {
        _timeZoneProvider = timeZoneProvider;
    }

    public string Format(IReadOnlyList<Event> events, IReadOnlyDictionary<int, int> eventAttachmentCount)
    {
        if (events.Count == 0)
        {
            return "Событий пока нет";
        }

        var builder = new StringBuilder();
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];

        for (var index = 0; index < events.Count; index++)
        {
            var eventEntity = events[index];
            builder.Append("📅 ");

            if (eventEntity.Kind == EventKind.OneOff && eventEntity.OccursAtUtc.HasValue)
            {
                var instant = Instant.FromDateTimeOffset(eventEntity.OccursAtUtc.Value);
                var zonedDateTime = instant.InZone(timeZone);
                var localDateTime = zonedDateTime.LocalDateTime;

                if (eventEntity.IsAllDay)
                {
                    builder.Append(localDateTime.Date.ToString("d MMMM", _culture));
                }
                else
                {
                    builder.Append(localDateTime.Date.ToString("d MMMM", _culture));
                    builder.Append(" ");
                    builder.Append(localDateTime.TimeOfDay.ToString("HH:mm", _culture));
                }
            }
            else if (eventEntity.Kind == EventKind.Yearly && eventEntity.Month.HasValue && eventEntity.Day.HasValue)
            {
                var date = new LocalDate(2000, eventEntity.Month.Value, eventEntity.Day.Value);
                builder.Append(date.ToString("d MMMM", _culture));

                if (!eventEntity.IsAllDay && eventEntity.TimeOfDay.HasValue)
                {
                    var time = LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks);
                    builder.Append(" ");
                    builder.Append(time.ToString("HH:mm", CultureInfo.InvariantCulture));
                }
            }

            builder.Append(" — ");
            builder.Append(eventEntity.Title);

            if (eventAttachmentCount.TryGetValue(eventEntity.Id, out var count) && count > 0)
            {
                if (count == 1)
                {
                    builder.Append(" 📎");
                }
                else
                {
                    builder.Append($" 📎({count})");
                }
            }

            if (eventEntity.Kind == EventKind.Yearly)
            {
                builder.Append(" (ежегодно)");
            }

            if (index < events.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}