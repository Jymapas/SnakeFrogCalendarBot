using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class DeleteBirthday
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<DeleteBirthday> _logger;

    public DeleteBirthday(
        IBirthdayRepository birthdayRepository,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<DeleteBirthday> logger)
    {
        _birthdayRepository = birthdayRepository;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
    }

    public async Task ExecuteAsync(int birthdayId, CancellationToken cancellationToken)
    {
        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            throw new InvalidOperationException($"Birthday with id {birthdayId} not found.");
        }

        await _birthdayRepository.DeleteAsync(birthdayId, cancellationToken);

        try
        {
            await _refreshLatestDigestPosts.ForBirthdayAsync(birthday, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after deleting birthday {BirthdayId}", birthday.Id);
        }
    }
}
