using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class CreateEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public CreateEvent(
        IEventRepository eventRepository,
        IClock clock,
        ITimeZoneProvider timeZoneProvider)
    {
        _eventRepository = eventRepository;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public Task ExecuteAsync(CreateEventCommand command, CancellationToken cancellationToken)
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

        return _eventRepository.AddAsync(eventEntity, cancellationToken);
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