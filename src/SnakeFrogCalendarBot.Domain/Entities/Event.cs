using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Domain.Entities;

public sealed class Event
{
    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public EventKind Kind { get; private set; }
    public bool IsAllDay { get; private set; }
    public DateTimeOffset? OccursAtUtc { get; private set; }
    public int? Month { get; private set; }
    public int? Day { get; private set; }
    public TimeSpan? TimeOfDay { get; private set; }
    public string? Description { get; private set; }
    public string? Place { get; private set; }
    public string? Link { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Event()
    {
    }

    public static Event CreateOneOff(
        string title,
        DateTimeOffset occursAtUtc,
        bool isAllDay,
        string? description,
        string? place,
        string? link,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        return new Event
        {
            Title = title.Trim(),
            Kind = EventKind.OneOff,
            IsAllDay = isAllDay,
            OccursAtUtc = occursAtUtc,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim(),
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public static Event CreateYearly(
        string title,
        int month,
        int day,
        TimeSpan? timeOfDay,
        bool isAllDay,
        string? description,
        string? place,
        string? link,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        if (month is < 1 or > 12)
        {
            throw new DomainException("Month must be between 1 and 12.");
        }

        if (day is < 1 or > 31)
        {
            throw new DomainException("Day must be between 1 and 31.");
        }

        return new Event
        {
            Title = title.Trim(),
            Kind = EventKind.Yearly,
            IsAllDay = isAllDay,
            Month = month,
            Day = day,
            TimeOfDay = timeOfDay,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim(),
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public void Touch(DateTime updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
    }
}