using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class ListBirthdays
{
    private readonly IBirthdayRepository _birthdayRepository;

    public ListBirthdays(IBirthdayRepository birthdayRepository)
    {
        _birthdayRepository = birthdayRepository;
    }

    public Task<IReadOnlyList<Birthday>> ExecuteAsync(CancellationToken cancellationToken)
    {
        return _birthdayRepository.ListAsync(cancellationToken);
    }
}
