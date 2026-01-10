using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class UpdateEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly IClock _clock;

    public UpdateEvent(IEventRepository eventRepository, IClock clock)
    {
        _eventRepository = eventRepository;
        _clock = clock;
    }

    public async Task ExecuteAsync(UpdateEventCommand command, CancellationToken cancellationToken)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(command.EventId, cancellationToken);
        if (eventEntity is null)
        {
            throw new InvalidOperationException($"Event with id {command.EventId} not found.");
        }

        var now = _clock.UtcNow;

        switch (command.Field)
        {
            case "title":
                eventEntity.UpdateTitle(command.Title!, now);
                break;
            case "description":
                eventEntity.UpdateDescription(command.Description, now);
                break;
            case "place":
                eventEntity.UpdatePlace(command.Place, now);
                break;
            case "link":
                eventEntity.UpdateLink(command.Link, now);
                break;
            case "occursAtUtc":
                if (!command.OccursAtUtc.HasValue)
                {
                    throw new InvalidOperationException("OccursAtUtc is required for this field.");
                }

                eventEntity.UpdateOccursAtUtc(command.OccursAtUtc.Value, now);
                break;
            case "yearlyDate":
                if (!command.Month.HasValue || !command.Day.HasValue)
                {
                    throw new InvalidOperationException("Month and Day are required for yearly date.");
                }

                eventEntity.UpdateYearlyDate(
                    command.Month.Value,
                    command.Day.Value,
                    command.TimeOfDay,
                    command.IsAllDay ?? false,
                    now);
                break;
            case "isAllDay":
                if (!command.IsAllDay.HasValue)
                {
                    throw new InvalidOperationException("IsAllDay is required for this field.");
                }

                eventEntity.UpdateIsAllDay(command.IsAllDay.Value, now);
                break;
            default:
                throw new InvalidOperationException($"Unknown field: {command.Field}");
        }

        await _eventRepository.UpdateAsync(eventEntity, cancellationToken);
    }
}

public sealed record UpdateEventCommand(
    int EventId,
    string Field,
    string? Title,
    string? Description,
    string? Place,
    string? Link,
    DateTimeOffset? OccursAtUtc,
    int? Month,
    int? Day,
    TimeSpan? TimeOfDay,
    bool? IsAllDay);