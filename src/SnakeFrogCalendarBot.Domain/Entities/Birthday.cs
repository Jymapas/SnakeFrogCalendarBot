using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Domain.Entities;

public sealed class Birthday
{
    public int Id { get; private set; }
    public string PersonName { get; private set; } = string.Empty;
    public int Day { get; private set; }
    public int Month { get; private set; }
    public int? BirthYear { get; private set; }
    public string? Contact { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Birthday()
    {
    }

    public Birthday(string personName, int day, int month, int? birthYear, string? contact, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            throw new DomainException("Person name is required.");
        }

        if (day is < 1 or > 31)
        {
            throw new DomainException("Day must be between 1 and 31.");
        }

        if (month is < 1 or > 12)
        {
            throw new DomainException("Month must be between 1 and 12.");
        }

        if (birthYear.HasValue && birthYear.Value <= 0)
        {
            throw new DomainException("Birth year must be a positive number.");
        }

        PersonName = personName.Trim();
        Day = day;
        Month = month;
        BirthYear = birthYear;
        Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void Touch(DateTime updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdatePersonName(string personName, DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            throw new DomainException("Person name is required.");
        }

        PersonName = personName.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateDate(int day, int month, DateTime updatedAtUtc)
    {
        if (day is < 1 or > 31)
        {
            throw new DomainException("Day must be between 1 and 31.");
        }

        if (month is < 1 or > 12)
        {
            throw new DomainException("Month must be between 1 and 12.");
        }

        Day = day;
        Month = month;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateBirthYear(int? birthYear, DateTime updatedAtUtc)
    {
        if (birthYear.HasValue && birthYear.Value <= 0)
        {
            throw new DomainException("Birth year must be a positive number.");
        }

        BirthYear = birthYear;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateContact(string? contact, DateTime updatedAtUtc)
    {
        Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }
}
