using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.UseCases.Events;

public sealed class ListUpcomingItems
{
    private readonly IEventRepository _eventRepository;

    public ListUpcomingItems(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public Task<IReadOnlyList<Event>> ExecuteAsync(CancellationToken cancellationToken)
    {
        return _eventRepository.ListUpcomingAsync(cancellationToken);
    }
}