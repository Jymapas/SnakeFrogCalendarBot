using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface IEventRepository
{
    Task AddAsync(Event eventEntity, CancellationToken cancellationToken);
    Task<IReadOnlyList<Event>> ListUpcomingAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Event>> ListAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Event>> ListUpcomingForEditAsync(CancellationToken cancellationToken);
    Task<Event?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task UpdateAsync(Event eventEntity, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}