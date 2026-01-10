using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface IAttachmentRepository
{
    Task AddAsync(Attachment attachment, CancellationToken cancellationToken);
    Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken);
    Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken);
    Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken);
    Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken);
}