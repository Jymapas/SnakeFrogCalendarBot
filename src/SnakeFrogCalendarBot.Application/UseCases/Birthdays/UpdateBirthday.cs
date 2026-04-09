using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class UpdateBirthday
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IClock _clock;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<UpdateBirthday> _logger;

    public UpdateBirthday(
        IBirthdayRepository birthdayRepository,
        IClock clock,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<UpdateBirthday> logger)
    {
        _birthdayRepository = birthdayRepository;
        _clock = clock;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
    }

    public async Task ExecuteAsync(UpdateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var birthday = await _birthdayRepository.GetByIdAsync(command.BirthdayId, cancellationToken);
        if (birthday is null)
        {
            throw new InvalidOperationException($"Birthday with id {command.BirthdayId} not found.");
        }

        var now = _clock.UtcNow;

        switch (command.Field)
        {
            case "personName":
                birthday.UpdatePersonName(command.PersonName!, now);
                break;
            case "date":
                if (!command.Day.HasValue || !command.Month.HasValue)
                {
                    throw new InvalidOperationException("Day and Month are required for date field.");
                }

                birthday.UpdateDate(command.Day.Value, command.Month.Value, now);
                break;
            case "birthYear":
                birthday.UpdateBirthYear(command.BirthYear, now);
                break;
            case "contact":
                birthday.UpdateContact(command.Contact, now);
                break;
            default:
                throw new InvalidOperationException($"Unknown field: {command.Field}");
        }

        await _birthdayRepository.UpdateAsync(birthday, cancellationToken);

        try
        {
            await _refreshLatestDigestPosts.ForBirthdayAsync(birthday, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after updating birthday {BirthdayId}", birthday.Id);
        }
    }
}

public sealed record UpdateBirthdayCommand(
    int BirthdayId,
    string Field,
    string? PersonName,
    int? Day,
    int? Month,
    int? BirthYear,
    string? Contact);
