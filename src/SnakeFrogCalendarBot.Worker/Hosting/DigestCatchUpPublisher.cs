using Microsoft.Extensions.Logging;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Worker.Config;
using IClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Worker.Hosting;

public sealed class DigestCatchUpPublisher
{
    private readonly DigestCatchUpPlanner _digestCatchUpPlanner;
    private readonly PublishDigest _publishDigest;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly AppOptions _options;
    private readonly ILogger<DigestCatchUpPublisher> _logger;

    public DigestCatchUpPublisher(
        DigestCatchUpPlanner digestCatchUpPlanner,
        PublishDigest publishDigest,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        AppOptions options,
        ILogger<DigestCatchUpPublisher> logger)
    {
        _digestCatchUpPlanner = digestCatchUpPlanner;
        _publishDigest = publishDigest;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _options = options;
        _logger = logger;
    }

    public async Task TryPublishAsync(CancellationToken cancellationToken)
    {
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowLocal = Instant.FromDateTimeUtc(_clock.UtcNow).InZone(timeZone).LocalDateTime;
        var dueDigests = _digestCatchUpPlanner.GetDueDigests(nowLocal, _options.TelegramChannelTriggerWindow);

        if (dueDigests.Count == 0)
        {
            _logger.LogInformation(
                "No missed digests to catch up within {TriggerWindowMinutes} minutes",
                _options.TelegramChannelTriggerWindow.TotalMinutes);
            return;
        }

        foreach (var dueDigest in dueDigests)
        {
            _logger.LogInformation(
                "Catch-up check for {DigestType} digest scheduled at {ScheduledAtLocal}",
                dueDigest.DigestType,
                dueDigest.ScheduledAtLocal);

            var published = dueDigest.DigestType switch
            {
                DigestType.Daily => await _publishDigest.PublishDailyAsync(dueDigest.TriggerDate, cancellationToken),
                DigestType.Weekly => await _publishDigest.PublishWeeklyAsync(dueDigest.TriggerDate, cancellationToken),
                DigestType.Monthly => await _publishDigest.PublishMonthlyAsync(dueDigest.TriggerDate, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported digest type: {dueDigest.DigestType}")
            };

            if (published)
            {
                _logger.LogInformation("Catch-up published {DigestType} digest", dueDigest.DigestType);
            }
        }
    }
}
