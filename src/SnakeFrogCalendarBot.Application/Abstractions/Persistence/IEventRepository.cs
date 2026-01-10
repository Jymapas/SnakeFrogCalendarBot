using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface IEventRepository
{
    Task AddAsync(Event eventEntity, CancellationToken cancellationToken);
    Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken);
}