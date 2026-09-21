using System.Globalization;
using System.Text;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Recurrence;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using IClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Application.Formatting;

public sealed class EventListFormatter
{
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly IClock _clock;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");

    public EventListFormatter(ITimeZoneProvider timeZoneProvider, IClock clock)
    {
        _timeZoneProvider = timeZoneProvider;
        _clock = clock;
    }

    public string Format(IReadOnlyList<Event> events, IReadOnlyDictionary<int, Attachment?> eventAttachments)
    {
        if (events.Count == 0)
        {
            return "Событий пока нет";
        }

        var builder = new StringBuilder();
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var today = Instant.FromDateTimeUtc(_clock.UtcNow).InZone(timeZone).Date;

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
            else if (eventEntity.IsRecurringSchedule)
            {
                var next = EventNextOccurrenceCalculator.TryGetNextLocalOccurrence(eventEntity, today, timeZone);
                if (next.HasValue)
                {
                    builder.Append(next.Value.Date.ToString("d MMMM", _culture));

                    if (next.Value.Time.HasValue)
                    {
                        builder.Append(" ");
                        builder.Append(next.Value.Time.Value.ToString("HH:mm", CultureInfo.InvariantCulture));
                    }
                }
            }

            builder.Append(" — ");
            builder.Append(eventEntity.Title);

            if (eventAttachments.TryGetValue(eventEntity.Id, out var attachment) && attachment != null)
            {
                builder.Append(" 📎 ");
                builder.Append(attachment.FileName);
            }

            var recurrence = RecurrenceDescriber.DescribeFull(eventEntity);
            if (recurrence is not null)
            {
                builder.Append(" (");
                builder.Append(recurrence);
                builder.Append(")");
            }

            if (index < events.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}