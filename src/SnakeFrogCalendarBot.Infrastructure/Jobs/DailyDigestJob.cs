using Microsoft.Extensions.Logging;
using Quartz;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using NodaTime;
using IClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class DailyDigestJob : IJob
{
    private readonly BuildDailyDigest _buildDailyDigest;
    private readonly SendDigest _sendDigest;
    private readonly DigestFormatter _formatter;
    private readonly INotificationRunRepository _notificationRunRepository;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ILogger<DailyDigestJob> _logger;

    public DailyDigestJob(
        BuildDailyDigest buildDailyDigest,
        SendDigest sendDigest,
        DigestFormatter formatter,
        INotificationRunRepository notificationRunRepository,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        ILogger<DailyDigestJob> logger)
    {
        _buildDailyDigest = buildDailyDigest;
        _sendDigest = sendDigest;
        _formatter = formatter;
        _notificationRunRepository = notificationRunRepository;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("DailyDigestJob started");

        try
        {
            var result = await _buildDailyDigest.ExecuteAsync(context.CancellationToken);

            var periodStartLocal = result.Date.AtMidnight().ToDateTimeUnspecified();
            var periodEndLocal = result.Date.At(LocalTime.MaxValue).ToDateTimeUnspecified();

            var exists = await _notificationRunRepository.ExistsAsync(
                DigestType.Daily,
                periodStartLocal,
                periodEndLocal,
                result.TimeZoneId,
                context.CancellationToken);

            if (exists)
            {
                _logger.LogInformation("Daily digest for {Date} already sent, skipping", result.Date);
                return;
            }

            var digestText = _formatter.FormatDaily(result.Date, result.Items);
            await _sendDigest.ExecuteAsync(digestText, context.CancellationToken);

            var notificationRun = new NotificationRun(
                DigestType.Daily,
                periodStartLocal,
                periodEndLocal,
                result.TimeZoneId,
                _clock.UtcNow);

            await _notificationRunRepository.AddAsync(notificationRun, context.CancellationToken);
            _logger.LogInformation("Daily digest for {Date} sent successfully", result.Date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DailyDigestJob");
            throw;
        }
    }
}