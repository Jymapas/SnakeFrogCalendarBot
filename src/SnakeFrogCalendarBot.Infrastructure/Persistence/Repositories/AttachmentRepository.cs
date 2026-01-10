using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;

public sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly CalendarDbContext _dbContext;

    public AttachmentRepository(CalendarDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Attachment?> GetCurrentByEventIdAsync(int eventId, CancellationToken cancellationToken)
    {
        return await _dbContext.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EventId == eventId && a.IsCurrent, cancellationToken);
    }

    public async Task<Attachment?> GetCurrentByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken)
    {
        return await _dbContext.Attachments
            .FirstOrDefaultAsync(a => a.EventId == eventId && a.IsCurrent, cancellationToken);
    }

    public async Task<Attachment?> GetLatestByEventIdForUpdateAsync(int eventId, CancellationToken cancellationToken)
    {
        return await _dbContext.Attachments
            .Where(a => a.EventId == eventId)
            .OrderByDescending(a => a.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Attachment>> GetByEventIdAsync(int eventId, CancellationToken cancellationToken)
    {
        return await _dbContext.Attachments
            .AsNoTracking()
            .Where(a => a.EventId == eventId)
            .OrderByDescending(a => a.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Attachment attachment, CancellationToken cancellationToken)
    {
        _dbContext.Attachments.Update(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}