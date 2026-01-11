using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface IBirthdayRepository
{
    Task AddAsync(Birthday birthday, CancellationToken cancellationToken);
    Task<IReadOnlyList<Birthday>> ListAsync(CancellationToken cancellationToken);
    Task<Birthday?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task UpdateAsync(Birthday birthday, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
