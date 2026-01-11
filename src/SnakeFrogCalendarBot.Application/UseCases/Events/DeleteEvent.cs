using SnakeFrogCalendarBot.Application.Abstractions.Persistence;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class DeleteEvent
{
    private readonly IEventRepository _eventRepository;

    public DeleteEvent(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task ExecuteAsync(int eventId, CancellationToken cancellationToken)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            throw new InvalidOperationException($"Event with id {eventId} not found.");
        }

        await _eventRepository.DeleteAsync(eventId, cancellationToken);
    }
}