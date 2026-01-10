using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface INotificationRunRepository
{
    Task AddAsync(NotificationRun notificationRun, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(
        DigestType digestType,
        DateTime periodStartLocal,
        DateTime periodEndLocal,
        string timeZoneId,
        CancellationToken cancellationToken);
}