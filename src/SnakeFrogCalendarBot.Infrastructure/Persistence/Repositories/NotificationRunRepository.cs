using Microsoft.EntityFrameworkCore;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;

public sealed class NotificationRunRepository : INotificationRunRepository
{
    private readonly CalendarDbContext _dbContext;

    public NotificationRunRepository(CalendarDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(NotificationRun notificationRun, CancellationToken cancellationToken)
    {
        _dbContext.NotificationRuns.Add(notificationRun);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        DigestType digestType,
        DateTime periodStartLocal,
        DateTime periodEndLocal,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.NotificationRuns
            .AsNoTracking()
            .AnyAsync(
                nr => nr.DigestType == digestType
                      && nr.PeriodStartLocal == periodStartLocal
                      && nr.PeriodEndLocal == periodEndLocal
                      && nr.TimeZoneId == timeZoneId,
                cancellationToken);
    }
}