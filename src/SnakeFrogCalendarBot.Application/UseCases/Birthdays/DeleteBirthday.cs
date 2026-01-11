using SnakeFrogCalendarBot.Application.Abstractions.Persistence;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class DeleteBirthday
{
    private readonly IBirthdayRepository _birthdayRepository;

    public DeleteBirthday(IBirthdayRepository birthdayRepository)
    {
        _birthdayRepository = birthdayRepository;
    }

    public async Task ExecuteAsync(int birthdayId, CancellationToken cancellationToken)
    {
        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            throw new InvalidOperationException($"Birthday with id {birthdayId} not found.");
        }

        await _birthdayRepository.DeleteAsync(birthdayId, cancellationToken);
    }
}