using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Birthdays;

public sealed class UpdateBirthday
{
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IClock _clock;

    public UpdateBirthday(IBirthdayRepository birthdayRepository, IClock clock)
    {
        _birthdayRepository = birthdayRepository;
        _clock = clock;
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