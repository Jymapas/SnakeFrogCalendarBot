using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.Abstractions.Persistence;

public interface ILatestDigestPostRepository
{
    Task UpsertAsync(
        DigestType digestType,
        int notificationRunId,
        int telegramMessageId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LatestDigestPostInfo>> ListAsync(CancellationToken cancellationToken);
}
