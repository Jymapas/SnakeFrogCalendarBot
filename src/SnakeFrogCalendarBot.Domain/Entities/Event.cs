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
    public int? DaysOfWeekMask { get; private set; }
    public int? IntervalCount { get; private set; }
    public RecurrenceUnit? IntervalUnit { get; private set; }
    public DateOnly? StartDateLocal { get; private set; }
    public DateOnly? RepeatUntilLocal { get; private set; }
    public string? Description { get; private set; }
    public string? Place { get; private set; }
    public string? Link { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Событие повторяется по расписанию с датой начала и опциональной датой окончания.
    /// </summary>
    public bool IsRecurringSchedule => Kind is EventKind.Weekly or EventKind.Interval;

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

    public void UpdateTitle(string title, DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        Title = title.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateDescription(string? description, DateTime updatedAtUtc)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdatePlace(string? place, DateTime updatedAtUtc)
    {
        Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateLink(string? link, DateTime updatedAtUtc)
    {
        Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateOccursAtUtc(DateTimeOffset occursAtUtc, DateTime updatedAtUtc)
    {
        if (Kind != EventKind.OneOff)
        {
            throw new DomainException("Can only update OccursAtUtc for OneOff events.");
        }

        OccursAtUtc = occursAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateYearlyDate(int month, int day, TimeSpan? timeOfDay, bool isAllDay, DateTime updatedAtUtc)
    {
        if (Kind != EventKind.Yearly)
        {
            throw new DomainException("Can only update yearly date for Yearly events.");
        }

        if (month is < 1 or > 12)
        {
            throw new DomainException("Month must be between 1 and 12.");
        }

        if (day is < 1 or > 31)
        {
            throw new DomainException("Day must be between 1 and 31.");
        }

        Month = month;
        Day = day;
        TimeOfDay = timeOfDay;
        IsAllDay = isAllDay;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static Event CreateWeekly(
        string title,
        int daysOfWeekMask,
        DateOnly startDateLocal,
        TimeSpan? timeOfDay,
        bool isAllDay,
        DateOnly? repeatUntilLocal,
        string? description,
        string? place,
        string? link,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        ValidateDaysOfWeekMask(daysOfWeekMask);
        ValidateRepeatUntil(startDateLocal, repeatUntilLocal);
        ValidateTimeOfDay(timeOfDay, isAllDay);

        return new Event
        {
            Title = title.Trim(),
            Kind = EventKind.Weekly,
            IsAllDay = isAllDay,
            DaysOfWeekMask = daysOfWeekMask,
            StartDateLocal = startDateLocal,
            RepeatUntilLocal = repeatUntilLocal,
            TimeOfDay = timeOfDay,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim(),
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public static Event CreateInterval(
        string title,
        int intervalCount,
        RecurrenceUnit intervalUnit,
        DateOnly startDateLocal,
        TimeSpan? timeOfDay,
        bool isAllDay,
        DateOnly? repeatUntilLocal,
        string? description,
        string? place,
        string? link,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        ValidateInterval(intervalCount, intervalUnit);
        ValidateRepeatUntil(startDateLocal, repeatUntilLocal);
        ValidateTimeOfDay(timeOfDay, isAllDay);

        return new Event
        {
            Title = title.Trim(),
            Kind = EventKind.Interval,
            IsAllDay = isAllDay,
            IntervalCount = intervalCount,
            IntervalUnit = intervalUnit,
            StartDateLocal = startDateLocal,
            RepeatUntilLocal = repeatUntilLocal,
            TimeOfDay = timeOfDay,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim(),
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };
    }

    public void UpdateWeeklySchedule(
        int daysOfWeekMask,
        DateOnly startDateLocal,
        TimeSpan? timeOfDay,
        bool isAllDay,
        DateTime updatedAtUtc)
    {
        if (Kind != EventKind.Weekly)
        {
            throw new DomainException("Can only update weekly schedule for Weekly events.");
        }

        ValidateDaysOfWeekMask(daysOfWeekMask);
        ValidateRepeatUntil(startDateLocal, RepeatUntilLocal);
        ValidateTimeOfDay(timeOfDay, isAllDay);

        DaysOfWeekMask = daysOfWeekMask;
        StartDateLocal = startDateLocal;
        TimeOfDay = timeOfDay;
        IsAllDay = isAllDay;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateIntervalSchedule(
        int intervalCount,
        RecurrenceUnit intervalUnit,
        DateOnly startDateLocal,
        TimeSpan? timeOfDay,
        bool isAllDay,
        DateTime updatedAtUtc)
    {
        if (Kind != EventKind.Interval)
        {
            throw new DomainException("Can only update interval schedule for Interval events.");
        }

        ValidateInterval(intervalCount, intervalUnit);
        ValidateRepeatUntil(startDateLocal, RepeatUntilLocal);
        ValidateTimeOfDay(timeOfDay, isAllDay);

        IntervalCount = intervalCount;
        IntervalUnit = intervalUnit;
        StartDateLocal = startDateLocal;
        TimeOfDay = timeOfDay;
        IsAllDay = isAllDay;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateRepeatUntil(DateOnly? repeatUntilLocal, DateTime updatedAtUtc)
    {
        if (!IsRecurringSchedule)
        {
            throw new DomainException("Can only update repeat boundary for recurring events.");
        }

        ValidateRepeatUntil(StartDateLocal!.Value, repeatUntilLocal);

        RepeatUntilLocal = repeatUntilLocal;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void ValidateDaysOfWeekMask(int daysOfWeekMask)
    {
        if (daysOfWeekMask is <= 0 or > 0b1111111)
        {
            throw new DomainException("Days of week mask must select at least one of seven days.");
        }
    }

    private static void ValidateInterval(int intervalCount, RecurrenceUnit intervalUnit)
    {
        if (intervalCount < 1)
        {
            throw new DomainException("Interval count must be at least 1.");
        }

        if (!Enum.IsDefined(intervalUnit))
        {
            throw new DomainException($"Unknown recurrence unit: {intervalUnit}.");
        }
    }

    private static void ValidateRepeatUntil(DateOnly startDateLocal, DateOnly? repeatUntilLocal)
    {
        if (repeatUntilLocal.HasValue && repeatUntilLocal.Value < startDateLocal)
        {
            throw new DomainException("Repeat boundary must not be earlier than the start date.");
        }
    }

    private static void ValidateTimeOfDay(TimeSpan? timeOfDay, bool isAllDay)
    {
        if (isAllDay && timeOfDay.HasValue)
        {
            throw new DomainException("All-day events must not carry a time of day.");
        }
    }

    public void UpdateIsAllDay(bool isAllDay, DateTime updatedAtUtc)
    {
        IsAllDay = isAllDay;
        UpdatedAtUtc = updatedAtUtc;
    }
}