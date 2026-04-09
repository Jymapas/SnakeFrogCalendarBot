using Microsoft.Extensions.Logging;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class PublishDigest
{
    private readonly DigestItemsProvider _digestItemsProvider;
    private readonly DigestFormatter _digestFormatter;
    private readonly SendDigest _sendDigest;
    private readonly INotificationRunRepository _notificationRunRepository;
    private readonly ILatestDigestPostRepository _latestDigestPostRepository;
    private readonly ITelegramPublisher _telegramPublisher;
    private readonly DigestPeriodCalculator _digestPeriodCalculator;
    private readonly AppClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ILogger<PublishDigest> _logger;

    public PublishDigest(
        DigestItemsProvider digestItemsProvider,
        DigestFormatter digestFormatter,
        SendDigest sendDigest,
        INotificationRunRepository notificationRunRepository,
        ILatestDigestPostRepository latestDigestPostRepository,
        ITelegramPublisher telegramPublisher,
        DigestPeriodCalculator digestPeriodCalculator,
        AppClock clock,
        ITimeZoneProvider timeZoneProvider,
        ILogger<PublishDigest> logger)
    {
        _digestItemsProvider = digestItemsProvider;
        _digestFormatter = digestFormatter;
        _sendDigest = sendDigest;
        _notificationRunRepository = notificationRunRepository;
        _latestDigestPostRepository = latestDigestPostRepository;
        _telegramPublisher = telegramPublisher;
        _digestPeriodCalculator = digestPeriodCalculator;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _logger = logger;
    }

    public Task<bool> PublishDailyAsync(LocalDate triggerDate, CancellationToken cancellationToken)
    {
        var date = _digestPeriodCalculator.CalculateDailyDate(triggerDate);
        return PublishAsync(DigestType.Daily, date, date, cancellationToken);
    }

    public Task<bool> PublishWeeklyAsync(LocalDate triggerDate, CancellationToken cancellationToken)
    {
        var (periodStart, periodEnd) = _digestPeriodCalculator.CalculateWeeklyPeriod(triggerDate);
        return PublishAsync(DigestType.Weekly, periodStart, periodEnd, cancellationToken);
    }

    public Task<bool> PublishMonthlyAsync(LocalDate triggerDate, CancellationToken cancellationToken)
    {
        var (periodStart, periodEnd) = _digestPeriodCalculator.CalculateMonthlyPeriod(triggerDate);
        return PublishAsync(DigestType.Monthly, periodStart, periodEnd, cancellationToken);
    }

    private async Task<bool> PublishAsync(
        DigestType digestType,
        LocalDate periodStart,
        LocalDate periodEnd,
        CancellationToken cancellationToken)
    {
        var timeZoneId = _timeZoneProvider.GetTimeZoneId();
        var periodStartLocal = periodStart.AtMidnight().ToDateTimeUnspecified();
        var periodEndLocal = periodEnd.At(LocalTime.MaxValue).ToDateTimeUnspecified();

        var exists = await _notificationRunRepository.ExistsAsync(
            digestType,
            periodStartLocal,
            periodEndLocal,
            timeZoneId,
            cancellationToken);

        if (exists)
        {
            _logger.LogInformation(
                "{DigestType} digest for {Start}-{End} already sent, skipping",
                digestType,
                periodStart,
                periodEnd);
            return false;
        }

        var previousLatestPost = digestType == DigestType.Monthly
            ? await _latestDigestPostRepository.GetByDigestTypeAsync(DigestType.Monthly, cancellationToken)
            : null;

        var items = await _digestItemsProvider.BuildAsync(periodStart, periodEnd, timeZoneId, cancellationToken);
        var digestText = digestType switch
        {
            DigestType.Daily => _digestFormatter.FormatDaily(periodStart, items),
            DigestType.Weekly => _digestFormatter.FormatWeekly(periodStart, periodEnd, items),
            DigestType.Monthly => _digestFormatter.FormatMonthly(periodStart, periodEnd, items),
            _ => throw new InvalidOperationException($"Unsupported digest type: {digestType}")
        };

        var digestAttachments = digestType == DigestType.Daily
            ? ExtractDailyAttachments(items)
            : [];

        var messageId = await _sendDigest.ExecuteAsync(digestText, digestAttachments, cancellationToken);
        var now = _clock.UtcNow;

        var notificationRun = new NotificationRun(
            digestType,
            periodStartLocal,
            periodEndLocal,
            timeZoneId,
            now);

        await _notificationRunRepository.AddAsync(notificationRun, cancellationToken);
        await _latestDigestPostRepository.UpsertAsync(
            digestType,
            notificationRun.Id,
            messageId,
            now,
            cancellationToken);

        if (digestType == DigestType.Monthly)
        {
            await TryUpdateMonthlyPinAsync(previousLatestPost?.TelegramMessageId, messageId, cancellationToken);
        }

        _logger.LogInformation(
            "{DigestType} digest for {Start}-{End} sent successfully",
            digestType,
            periodStart,
            periodEnd);

        return true;
    }

    private static IReadOnlyList<DigestAttachmentDto> ExtractDailyAttachments(IReadOnlyList<CalendarItemDto> items)
    {
        return items
            .Where(item => item.Type == CalendarItemType.Event)
            .SelectMany(item => item.Attachments)
            .DistinctBy(attachment => attachment.TelegramFileId)
            .ToList();
    }

    private async Task TryUpdateMonthlyPinAsync(
        int? previousMessageId,
        int newMessageId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telegramPublisher.PinMessageAsync(newMessageId, disableNotification: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pin latest monthly digest message {MessageId}", newMessageId);
            return;
        }

        if (!previousMessageId.HasValue || previousMessageId.Value == newMessageId)
        {
            return;
        }

        try
        {
            await _telegramPublisher.UnpinMessageAsync(previousMessageId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to unpin previous monthly digest message {MessageId}",
                previousMessageId.Value);
        }
    }
}
