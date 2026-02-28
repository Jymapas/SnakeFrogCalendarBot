using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class RefreshLatestDigestPosts
{
    private readonly ILatestDigestPostRepository _latestDigestPostRepository;
    private readonly DigestItemsProvider _digestItemsProvider;
    private readonly DigestFormatter _digestFormatter;
    private readonly ITelegramPublisher _telegramPublisher;

    public RefreshLatestDigestPosts(
        ILatestDigestPostRepository latestDigestPostRepository,
        DigestItemsProvider digestItemsProvider,
        DigestFormatter digestFormatter,
        ITelegramPublisher telegramPublisher)
    {
        _latestDigestPostRepository = latestDigestPostRepository;
        _digestItemsProvider = digestItemsProvider;
        _digestFormatter = digestFormatter;
        _telegramPublisher = telegramPublisher;
    }

    public Task ForBirthdayAsync(Birthday birthday, CancellationToken cancellationToken)
    {
        return RefreshAsync(
            post => DigestOccurrenceResolver.TryResolve(
                birthday,
                ToLocalDate(post.PeriodStartLocal),
                ToLocalDate(post.PeriodEndLocal),
                out _),
            cancellationToken);
    }

    public Task ForEventAsync(Event eventEntity, CancellationToken cancellationToken)
    {
        return RefreshAsync(
            post =>
            {
                var timeZone = DateTimeZoneProviders.Tzdb[post.TimeZoneId];
                return DigestOccurrenceResolver.TryResolve(
                    eventEntity,
                    ToLocalDate(post.PeriodStartLocal),
                    ToLocalDate(post.PeriodEndLocal),
                    timeZone,
                    out _,
                    out _);
            },
            cancellationToken);
    }

    private async Task RefreshAsync(
        Func<LatestDigestPostInfo, bool> shouldRefresh,
        CancellationToken cancellationToken)
    {
        var latestPosts = await _latestDigestPostRepository.ListAsync(cancellationToken);

        foreach (var latestPost in latestPosts)
        {
            if (!shouldRefresh(latestPost))
            {
                continue;
            }

            var periodStart = ToLocalDate(latestPost.PeriodStartLocal);
            var periodEnd = ToLocalDate(latestPost.PeriodEndLocal);
            var items = await _digestItemsProvider.BuildAsync(
                periodStart,
                periodEnd,
                latestPost.TimeZoneId,
                cancellationToken);

            var digestText = latestPost.DigestType switch
            {
                DigestType.Daily => _digestFormatter.FormatDaily(periodStart, items),
                DigestType.Weekly => _digestFormatter.FormatWeekly(periodStart, periodEnd, items),
                DigestType.Monthly => _digestFormatter.FormatMonthly(periodStart, periodEnd, items),
                _ => throw new InvalidOperationException($"Unsupported digest type: {latestPost.DigestType}")
            };

            await _telegramPublisher.EditMessageAsync(
                latestPost.TelegramMessageId,
                digestText,
                cancellationToken);
        }
    }

    private static LocalDate ToLocalDate(DateTime value)
    {
        return LocalDateTime.FromDateTime(value).Date;
    }
}
