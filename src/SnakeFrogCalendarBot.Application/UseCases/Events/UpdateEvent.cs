using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class UpdateEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly IClock _clock;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<UpdateEvent> _logger;

    public UpdateEvent(
        IEventRepository eventRepository,
        IClock clock,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<UpdateEvent> logger)
    {
        _eventRepository = eventRepository;
        _clock = clock;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
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
            case "weeklySchedule":
                if (!command.DaysOfWeekMask.HasValue || !command.StartDateLocal.HasValue)
                {
                    throw new InvalidOperationException("DaysOfWeekMask and StartDateLocal are required for weekly schedule.");
                }

                eventEntity.UpdateWeeklySchedule(
                    command.DaysOfWeekMask.Value,
                    command.StartDateLocal.Value,
                    command.TimeOfDay,
                    command.IsAllDay ?? false,
                    now);
                break;
            case "intervalSchedule":
                if (!command.IntervalCount.HasValue || !command.IntervalUnit.HasValue || !command.StartDateLocal.HasValue)
                {
                    throw new InvalidOperationException("IntervalCount, IntervalUnit and StartDateLocal are required for interval schedule.");
                }

                eventEntity.UpdateIntervalSchedule(
                    command.IntervalCount.Value,
                    command.IntervalUnit.Value,
                    command.StartDateLocal.Value,
                    command.TimeOfDay,
                    command.IsAllDay ?? false,
                    now);
                break;
            case "repeatUntil":
                eventEntity.UpdateRepeatUntil(command.RepeatUntilLocal, now);
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

        try
        {
            // Изменение расписания может вынести событие из уже опубликованного поста,
            // а ForEventAsync ищет посты по НОВОМУ расписанию и такой пост не найдёт.
            var refreshTask = command.Field is "occursAtUtc" or "yearlyDate"
                or "weeklySchedule" or "intervalSchedule" or "repeatUntil"
                ? _refreshLatestDigestPosts.ForAllAsync(cancellationToken)
                : _refreshLatestDigestPosts.ForEventAsync(eventEntity, cancellationToken);
            await refreshTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after updating event {EventId}", eventEntity.Id);
        }
    }
}

public sealed record UpdateEventCommand
{
    public required int EventId { get; init; }
    public required string Field { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Place { get; init; }
    public string? Link { get; init; }
    public DateTimeOffset? OccursAtUtc { get; init; }
    public int? Month { get; init; }
    public int? Day { get; init; }
    public TimeSpan? TimeOfDay { get; init; }
    public bool? IsAllDay { get; init; }
    public int? DaysOfWeekMask { get; init; }
    public int? IntervalCount { get; init; }
    public RecurrenceUnit? IntervalUnit { get; init; }
    public DateOnly? StartDateLocal { get; init; }
    public DateOnly? RepeatUntilLocal { get; init; }
}
