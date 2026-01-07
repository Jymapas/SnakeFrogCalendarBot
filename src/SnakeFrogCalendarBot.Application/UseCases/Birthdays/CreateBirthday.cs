using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class CreateBirthday
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IClock _clock;

    public CreateBirthday(IBirthdayRepository birthdayRepository, IClock clock)
    {
        _birthdayRepository = birthdayRepository;
        _clock = clock;
    }

    public Task ExecuteAsync(CreateBirthdayCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var birthday = new Birthday(
            command.PersonName,
            command.Day,
            command.Month,
            command.BirthYear,
            command.Contact,
            now);

        return _birthdayRepository.AddAsync(birthday, cancellationToken);
    }
}

public sealed record CreateBirthdayCommand(
    string PersonName,
    int Day,
    int Month,
    int? BirthYear,
    string? Contact);
