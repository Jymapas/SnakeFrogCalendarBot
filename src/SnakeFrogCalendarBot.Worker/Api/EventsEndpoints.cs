using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        AppOptions options,
        IDateTimeParser dateParser,
        IBirthdayDateParser birthdayDateParser,
        ITimeZoneProvider timeZoneProvider,
        CreateEvent createEvent,
        CancellationToken cancellationToken)
    {
        var userId = TelegramInitDataValidator.Validate(request, options);
        if (userId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Title))
            return Results.BadRequest("title is required");

        if (string.IsNullOrWhiteSpace(body.Date))
            return Results.BadRequest("date is required");

        CreateEventCommand command;
        if (body.IsYearly)
        {
            if (!birthdayDateParser.TryParseMonthDay(body.Date, out var day, out var month))
                return Results.BadRequest($"Cannot parse date '{body.Date}' as yearly event (expected format: '15 марта' or '15.03')");

            command = new CreateEventCommand(
                Title: body.Title,
                Kind: EventKind.Yearly,
                IsAllDay: true,
                OccursAtUtc: null,
                Month: month,
                Day: day,
                TimeOfDay: null,
                Description: body.Description,
                Place: body.Place,
                Link: body.Link);
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

            command = new CreateEventCommand(
                Title: body.Title,
                Kind: EventKind.OneOff,
                IsAllDay: isAllDay,
                OccursAtUtc: occursAtUtc,
                Month: null,
                Day: null,
                TimeOfDay: null,
                Description: body.Description,
                Place: body.Place,
                Link: body.Link);
        }

        await createEvent.ExecuteAsync(command, cancellationToken);
        return Results.Ok();
    }
}

public sealed record CreateEventRequest(
    string Title,
    string Date,
    bool IsYearly,
    string? Description,
    string? Place,
    string? Link);
