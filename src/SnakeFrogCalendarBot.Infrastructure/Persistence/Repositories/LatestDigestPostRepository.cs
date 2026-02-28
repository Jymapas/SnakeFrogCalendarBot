using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;

public sealed class LatestDigestPostRepository : ILatestDigestPostRepository
{
    private readonly CalendarDbContext _dbContext;

    public LatestDigestPostRepository(CalendarDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(
        DigestType digestType,
        int notificationRunId,
        int telegramMessageId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.LatestDigestPosts
            .FirstOrDefaultAsync(post => post.DigestType == digestType, cancellationToken);

        if (existing is null)
        {
            _dbContext.LatestDigestPosts.Add(new LatestDigestPost(
                digestType,
                notificationRunId,
                telegramMessageId,
                updatedAtUtc));
        }
        else
        {
            existing.Update(notificationRunId, telegramMessageId, updatedAtUtc);
            _dbContext.LatestDigestPosts.Update(existing);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LatestDigestPostInfo>> ListAsync(CancellationToken cancellationToken)
    {
        return await (
                from post in _dbContext.LatestDigestPosts.AsNoTracking()
                join run in _dbContext.NotificationRuns.AsNoTracking()
                    on post.NotificationRunId equals run.Id
                select new LatestDigestPostInfo(
                    post.DigestType,
                    post.TelegramMessageId,
                    run.PeriodStartLocal,
                    run.PeriodEndLocal,
                    run.TimeZoneId))
            .ToListAsync(cancellationToken);
    }
}
