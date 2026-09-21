using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class CreateEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<CreateEvent> _logger;

    public CreateEvent(
        IEventRepository eventRepository,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<CreateEvent> logger)
    {
        _eventRepository = eventRepository;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
    }

    public async Task ExecuteAsync(CreateEventCommand command, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var eventEntity = command.Kind switch
        {
            EventKind.OneOff => CreateOneOff(command, now),
            EventKind.Yearly => CreateYearly(command, now),
            EventKind.Weekly => CreateWeekly(command, now),
            EventKind.Interval => CreateInterval(command, now),
            _ => throw new InvalidOperationException($"Unsupported event kind: {command.Kind}.")
        };

        await _eventRepository.AddAsync(eventEntity, cancellationToken);

        try
        {
            await _refreshLatestDigestPosts.ForEventAsync(eventEntity, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after creating event {Title}", eventEntity.Title);
        }
    }

    private static Event CreateOneOff(CreateEventCommand command, DateTime now)
    {
        if (!command.OccursAtUtc.HasValue)
        {
            throw new InvalidOperationException("OccursAtUtc is required for OneOff events.");
        }

        return Event.CreateOneOff(
            command.Title,
            command.OccursAtUtc.Value,
            command.IsAllDay,
            command.Description,
            command.Place,
            command.Link,
            now);
    }

    private static Event CreateYearly(CreateEventCommand command, DateTime now)
    {
        if (!command.Month.HasValue || !command.Day.HasValue)
        {
            throw new InvalidOperationException("Month and Day are required for Yearly events.");
        }

        return Event.CreateYearly(
            command.Title,
            command.Month.Value,
            command.Day.Value,
            command.TimeOfDay,
            command.IsAllDay,
            command.Description,
            command.Place,
            command.Link,
            now);
    }

    private static Event CreateWeekly(CreateEventCommand command, DateTime now)
    {
        if (!command.DaysOfWeekMask.HasValue)
        {
            throw new InvalidOperationException("DaysOfWeekMask is required for Weekly events.");
        }

        if (!command.StartDateLocal.HasValue)
        {
            throw new InvalidOperationException("StartDateLocal is required for Weekly events.");
        }

        return Event.CreateWeekly(
            command.Title,
            command.DaysOfWeekMask.Value,
            command.StartDateLocal.Value,
            command.TimeOfDay,
            command.IsAllDay,
            command.RepeatUntilLocal,
            command.Description,
            command.Place,
            command.Link,
            now);
    }

    private static Event CreateInterval(CreateEventCommand command, DateTime now)
    {
        if (!command.IntervalCount.HasValue || !command.IntervalUnit.HasValue)
        {
            throw new InvalidOperationException("IntervalCount and IntervalUnit are required for Interval events.");
        }

        if (!command.StartDateLocal.HasValue)
        {
            throw new InvalidOperationException("StartDateLocal is required for Interval events.");
        }

        return Event.CreateInterval(
            command.Title,
            command.IntervalCount.Value,
            command.IntervalUnit.Value,
            command.StartDateLocal.Value,
            command.TimeOfDay,
            command.IsAllDay,
            command.RepeatUntilLocal,
            command.Description,
            command.Place,
            command.Link,
            now);
    }
}

public sealed record CreateEventCommand
{
    public required string Title { get; init; }
    public required EventKind Kind { get; init; }
    public bool IsAllDay { get; init; }
    public DateTimeOffset? OccursAtUtc { get; init; }
    public int? Month { get; init; }
    public int? Day { get; init; }
    public TimeSpan? TimeOfDay { get; init; }
    public int? DaysOfWeekMask { get; init; }
    public int? IntervalCount { get; init; }
    public RecurrenceUnit? IntervalUnit { get; init; }
    public DateOnly? StartDateLocal { get; init; }
    public DateOnly? RepeatUntilLocal { get; init; }
    public string? Description { get; init; }
    public string? Place { get; init; }
    public string? Link { get; init; }
}
