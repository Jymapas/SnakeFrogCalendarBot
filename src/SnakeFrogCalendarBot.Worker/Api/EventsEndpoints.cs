using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Worker.Config;

namespace SnakeFrogCalendarBot.Worker.Api;

public static class EventsEndpoints
{
    public static async Task<IResult> Handle(
        [FromBody] CreateEventRequest body,
        HttpRequest request,
        [FromServices] AppOptions options,
        [FromServices] IDateTimeParser dateParser,
        [FromServices] IBirthdayDateParser birthdayDateParser,
        [FromServices] ITimeZoneProvider timeZoneProvider,
        [FromServices] CreateEvent createEvent,
        [FromServices] MiniAppTokenService tokenService,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("EventsEndpoint");
        var userId = TelegramInitDataValidator.Validate(request, options, tokenService, logger);
        if (userId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Title))
            return Results.BadRequest("title is required");

        if (string.IsNullOrWhiteSpace(body.Date))
            return Results.BadRequest("date is required");

        var recurrence = (body.Recurrence ?? (body.IsYearly ? "yearly" : "oneoff")).Trim().ToLowerInvariant();

        if (recurrence is "weekly" or "interval")
        {
            var recurringCommand = BuildRecurringCommand(body, recurrence, out var error);
            if (recurringCommand is null)
                return Results.BadRequest(error);

            await createEvent.ExecuteAsync(recurringCommand, cancellationToken);
            return Results.Ok();
        }

        CreateEventCommand command;
        if (recurrence == "yearly")
        {
            if (!birthdayDateParser.TryParseMonthDay(body.Date, out var day, out var month))
                return Results.BadRequest($"Cannot parse date '{body.Date}' as yearly event (expected format: '15 марта' or '15.03')");

            command = new CreateEventCommand
            {
                Title = body.Title,
                Kind = EventKind.Yearly,
                IsAllDay = true,
                Month = month,
                Day = day,
                Description = body.Description,
                Place = body.Place,
                Link = body.Link
            };
        }
        else
        {
            if (!dateParser.TryParse(body.Date, out var parseResult) || parseResult is null)
                return Results.BadRequest($"Cannot parse date '{body.Date}' (expected formats: '25 декабря 2025', '25.12.2025 14:00', '2025-12-25 14:00')");

            var tzId = timeZoneProvider.GetTimeZoneId();
            var tz = DateTimeZoneProviders.Tzdb[tzId];

            var localDate = new LocalDate(parseResult.Year, parseResult.Month, parseResult.Day);
            LocalDateTime localDateTime;

            if (parseResult.Hour is null || parseResult.Minute is null)
            {
                localDateTime = localDate.AtMidnight();
            }
            else
            {
                localDateTime = localDate.At(new LocalTime(parseResult.Hour.Value, parseResult.Minute.Value));
            }

            var occursAtUtc = localDateTime.InZoneLeniently(tz).ToInstant().ToDateTimeOffset();
            var isAllDay = parseResult.Hour is null;

            command = new CreateEventCommand
            {
                Title = body.Title,
                Kind = EventKind.OneOff,
                IsAllDay = isAllDay,
                OccursAtUtc = occursAtUtc,
                Description = body.Description,
                Place = body.Place,
                Link = body.Link
            };
        }

        await createEvent.ExecuteAsync(command, cancellationToken);
        return Results.Ok();
    }

    private static CreateEventCommand? BuildRecurringCommand(
        CreateEventRequest body,
        string recurrence,
        out string? error)
    {
        error = null;

        // Date для повторяющегося события — дата начала, опционально со временем: «2026-03-02 19:00».
        var dateParts = body.Date.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!DateOnly.TryParse(dateParts[0], out var startDate))
        {
            error = $"Cannot parse start date '{body.Date}' (expected ISO format: '2026-03-02')";
            return null;
        }

        var isAllDay = true;
        TimeSpan? timeOfDay = null;
        if (dateParts.Length > 1)
        {
            if (!TimeSpan.TryParse(dateParts[1], out var parsedTime))
            {
                error = $"Cannot parse time '{dateParts[1]}' (expected 'HH:mm')";
                return null;
            }

            isAllDay = false;
            timeOfDay = parsedTime;
        }

        DateOnly? repeatUntil = null;
        if (!string.IsNullOrWhiteSpace(body.RepeatUntil))
        {
            if (!DateOnly.TryParse(body.RepeatUntil, out var parsedUntil))
            {
                error = $"Cannot parse repeatUntil '{body.RepeatUntil}' (expected ISO format: '2026-12-31')";
                return null;
            }

            repeatUntil = parsedUntil;
        }

        if (recurrence == "weekly")
        {
            var mask = 0;
            foreach (var day in body.DaysOfWeek ?? [])
            {
                if (day is < 1 or > 7)
                {
                    error = $"dayOfWeek {day} is out of range (1..7)";
                    return null;
                }

                mask |= 1 << (day - 1);
            }

            if (mask == 0)
            {
                error = "daysOfWeek is required for weekly events";
                return null;
            }

            return new CreateEventCommand
            {
                Title = body.Title,
                Kind = EventKind.Weekly,
                IsAllDay = isAllDay,
                TimeOfDay = timeOfDay,
                DaysOfWeekMask = mask,
                StartDateLocal = startDate,
                RepeatUntilLocal = repeatUntil,
                Description = body.Description,
                Place = body.Place,
                Link = body.Link
            };
        }

        if (body.IntervalCount is null or < 1)
        {
            error = "intervalCount must be at least 1";
            return null;
        }

        if (!Enum.TryParse<RecurrenceUnit>(body.IntervalUnit, ignoreCase: true, out var unit))
        {
            error = $"Unknown intervalUnit '{body.IntervalUnit}' (expected days, weeks, months or years)";
            return null;
        }

        return new CreateEventCommand
        {
            Title = body.Title,
            Kind = EventKind.Interval,
            IsAllDay = isAllDay,
            TimeOfDay = timeOfDay,
            IntervalCount = body.IntervalCount,
            IntervalUnit = unit,
            StartDateLocal = startDate,
            RepeatUntilLocal = repeatUntil,
            Description = body.Description,
            Place = body.Place,
            Link = body.Link
        };
    }
}

/// <summary>
/// Тело запроса mini app. Поля повторения необязательны: фронтенд деплоится отдельно
/// (GitHub Pages), поэтому старый бандл продолжает присылать только IsYearly.
/// </summary>
public sealed record CreateEventRequest(
    string Title,
    string Date,
    bool IsYearly,
    string? Description,
    string? Place,
    string? Link)
{
    /// <summary>oneoff | yearly | weekly | interval. Пусто — используется IsYearly.</summary>
    public string? Recurrence { get; init; }

    /// <summary>Дни недели для weekly: 1 — понедельник, 7 — воскресенье.</summary>
    public int[]? DaysOfWeek { get; init; }

    public int? IntervalCount { get; init; }

    /// <summary>days | weeks | months | years.</summary>
    public string? IntervalUnit { get; init; }

    /// <summary>Дата окончания повторения, ISO yyyy-MM-dd. Пусто — бессрочно.</summary>
    public string? RepeatUntil { get; init; }
}
