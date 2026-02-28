using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class CreateBirthday
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IClock _clock;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<CreateBirthday> _logger;

    public CreateBirthday(
        IBirthdayRepository birthdayRepository,
        IClock clock,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<CreateBirthday> logger)
    {
        _birthdayRepository = birthdayRepository;
        _clock = clock;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
    }

    public async Task ExecuteAsync(CreateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var birthday = new Birthday(
            command.PersonName,
            command.Day,
            command.Month,
            command.BirthYear,
            command.Contact,
            now);

        await _birthdayRepository.AddAsync(birthday, cancellationToken);

        try
        {
            await _refreshLatestDigestPosts.ForBirthdayAsync(birthday, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after creating birthday {PersonName}", birthday.PersonName);
        }
    }
}

public sealed record CreateBirthdayCommand(
    string PersonName,
    int Day,
    int Month,
    int? BirthYear,
    string? Contact);
