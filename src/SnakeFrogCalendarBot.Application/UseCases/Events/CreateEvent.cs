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
        Event eventEntity;

        if (command.Kind == EventKind.OneOff)
        {
            if (!command.OccursAtUtc.HasValue)
            {
                throw new InvalidOperationException("OccursAtUtc is required for OneOff events.");
            }

            eventEntity = Event.CreateOneOff(
                command.Title,
                command.OccursAtUtc.Value,
                command.IsAllDay,
                command.Description,
                command.Place,
                command.Link,
                now);
        }
        else
        {
            if (!command.Month.HasValue || !command.Day.HasValue)
            {
                throw new InvalidOperationException("Month and Day are required for Yearly events.");
            }

            eventEntity = Event.CreateYearly(
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
}

public sealed record CreateEventCommand(
    string Title,
    EventKind Kind,
    bool IsAllDay,
    DateTimeOffset? OccursAtUtc,
    int? Month,
    int? Day,
    TimeSpan? TimeOfDay,
    string? Description,
    string? Place,
    string? Link);
