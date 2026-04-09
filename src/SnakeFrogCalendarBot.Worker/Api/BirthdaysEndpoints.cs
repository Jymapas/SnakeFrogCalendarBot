using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Worker.Config;

namespace SnakeFrogCalendarBot.Worker.Api;

public static class BirthdaysEndpoints
{
    public static async Task<IResult> Handle(
        [FromBody] CreateBirthdayRequest body,
        HttpRequest request,
        AppOptions options,
        IBirthdayDateParser birthdayDateParser,
        CreateBirthday createBirthday,
        CancellationToken cancellationToken)
    {
        var userId = TelegramInitDataValidator.Validate(request, options);
        if (userId is null)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.PersonName))
            return Results.BadRequest("personName is required");

        if (string.IsNullOrWhiteSpace(body.Date))
            return Results.BadRequest("date is required");

        if (!birthdayDateParser.TryParseMonthDay(body.Date, out var day, out var month))
            return Results.BadRequest($"Cannot parse date '{body.Date}' (expected formats: '15 марта', '15.03', '15.03.1990')");

        int? birthYear = null;
        if (!string.IsNullOrWhiteSpace(body.BirthYear))
        {
            if (!int.TryParse(body.BirthYear, out var year) || year <= 0)
                return Results.BadRequest("birthYear must be a positive number");
            birthYear = year;
        }

        var command = new CreateBirthdayCommand(
            PersonName: body.PersonName.Trim(),
            Day: day,
            Month: month,
            BirthYear: birthYear,
            Contact: string.IsNullOrWhiteSpace(body.Contact) ? null : body.Contact.Trim());

        await createBirthday.ExecuteAsync(command, cancellationToken);
        return Results.Ok();
    }
}

public sealed record CreateBirthdayRequest(
    string PersonName,
    string Date,
    string? BirthYear,
    string? Contact);
