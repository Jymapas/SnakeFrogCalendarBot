using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using Microsoft.Extensions.Logging;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class DeleteEvent
{
    private readonly IEventRepository _eventRepository;
    private readonly RefreshLatestDigestPosts _refreshLatestDigestPosts;
    private readonly ILogger<DeleteEvent> _logger;

    public DeleteEvent(
        IEventRepository eventRepository,
        RefreshLatestDigestPosts refreshLatestDigestPosts,
        ILogger<DeleteEvent> logger)
    {
        _eventRepository = eventRepository;
        _refreshLatestDigestPosts = refreshLatestDigestPosts;
        _logger = logger;
    }

    public async Task ExecuteAsync(int eventId, CancellationToken cancellationToken)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            throw new InvalidOperationException($"Event with id {eventId} not found.");
        }

        await _eventRepository.DeleteAsync(eventId, cancellationToken);

        try
        {
            await _refreshLatestDigestPosts.ForEventAsync(eventEntity, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh latest digest posts after deleting event {EventId}", eventEntity.Id);
        }
    }
}
