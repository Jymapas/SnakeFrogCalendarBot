using NodaTime;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public enum EventCommandBuildFailure
{
    None,
    MissingFields,
    MissingYear,
    MissingSchedule
}

/// <summary>
/// Собирает команду создания события из данных диалога. Общий код для пошагового
/// и многострочного ввода: различаются они только текстами ошибок.
/// </summary>
public static class EventCommandBuilder
{
    public static EventCommandBuildFailure TryBuild(
        EventConversationData data,
        DateTimeZone timeZone,
        out CreateEventCommand? command)
    {
        command = null;

        if (string.IsNullOrWhiteSpace(data.Title) || data.Kind is null)
        {
            return EventCommandBuildFailure.MissingFields;
        }

        if (data.Kind is "Weekly" or "Interval")
        {
            return TryBuildRecurring(data, out command);
        }

        if (data.Month is null || data.Day is null)
        {
            return EventCommandBuildFailure.MissingFields;
        }

        if (data.Kind == "OneOff")
        {
            if (data.Year is null)
            {
                return EventCommandBuildFailure.MissingYear;
            }

            command = new CreateEventCommand
            {
                Title = data.Title,
                Kind = EventKind.OneOff,
                IsAllDay = data.IsAllDay == true,
                OccursAtUtc = ResolveOccursAtUtc(data, timeZone),
                Description = data.Description,
                Place = data.Place,
                Link = data.Link
            };

            return EventCommandBuildFailure.None;
        }

        command = new CreateEventCommand
        {
            Title = data.Title,
            Kind = EventKind.Yearly,
            IsAllDay = data.IsAllDay == true,
            Month = data.Month,
            Day = data.Day,
            TimeOfDay = ResolveTimeOfDay(data),
            Description = data.Description,
            Place = data.Place,
            Link = data.Link
        };

        return EventCommandBuildFailure.None;
    }

    private static EventCommandBuildFailure TryBuildRecurring(
        EventConversationData data,
        out CreateEventCommand? command)
    {
        command = null;

        if (data.StartDate is null)
        {
            return EventCommandBuildFailure.MissingSchedule;
        }

        var isAllDay = data.IsAllDay == true;
        var timeOfDay = ResolveTimeOfDay(data);

        if (data.Kind == "Weekly")
        {
            if (data.DaysOfWeekMask is null or 0)
            {
                return EventCommandBuildFailure.MissingSchedule;
            }

            command = new CreateEventCommand
            {
                Title = data.Title!,
                Kind = EventKind.Weekly,
                IsAllDay = isAllDay,
                TimeOfDay = timeOfDay,
                DaysOfWeekMask = data.DaysOfWeekMask,
                StartDateLocal = data.StartDate,
                RepeatUntilLocal = data.RepeatUntil,
                Description = data.Description,
                Place = data.Place,
                Link = data.Link
            };

            return EventCommandBuildFailure.None;
        }

        if (data.IntervalCount is null or < 1
            || !Enum.TryParse<RecurrenceUnit>(data.IntervalUnit, out var unit))
        {
            return EventCommandBuildFailure.MissingSchedule;
        }

        command = new CreateEventCommand
        {
            Title = data.Title!,
            Kind = EventKind.Interval,
            IsAllDay = isAllDay,
            TimeOfDay = timeOfDay,
            IntervalCount = data.IntervalCount,
            IntervalUnit = unit,
            StartDateLocal = data.StartDate,
            RepeatUntilLocal = data.RepeatUntil,
            Description = data.Description,
            Place = data.Place,
            Link = data.Link
        };

        return EventCommandBuildFailure.None;
    }

    private static DateTimeOffset ResolveOccursAtUtc(EventConversationData data, DateTimeZone timeZone)
    {
        var localDate = new LocalDate(data.Year!.Value, data.Month!.Value, data.Day!.Value);

        LocalDateTime localDateTime;
        if (data.IsAllDay == true)
        {
            localDateTime = localDate.AtMidnight();
        }
        else
        {
            var time = data.Hour.HasValue && data.Minute.HasValue
                ? new LocalTime(data.Hour.Value, data.Minute.Value)
                : LocalTime.Midnight;
            localDateTime = localDate.At(time);
        }

        return localDateTime.InZoneLeniently(timeZone).ToInstant().ToDateTimeOffset();
    }

    private static TimeSpan? ResolveTimeOfDay(EventConversationData data)
    {
        if (data.IsAllDay == true || !data.Hour.HasValue || !data.Minute.HasValue)
        {
            return null;
        }

        return new TimeSpan(data.Hour.Value, data.Minute.Value, 0);
    }
}
