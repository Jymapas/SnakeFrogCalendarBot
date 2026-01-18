using Microsoft.Extensions.Logging;
using Quartz;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;

namespace SnakeFrogCalendarBot.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class WeeklyDigestJob : IJob
{
    private readonly BuildWeeklyDigest _buildWeeklyDigest;
    private readonly SendDigest _sendDigest;
    private readonly DigestFormatter _formatter;
    private readonly INotificationRunRepository _notificationRunRepository;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ILogger<WeeklyDigestJob> _logger;

    public WeeklyDigestJob(
        BuildWeeklyDigest buildWeeklyDigest,
        SendDigest sendDigest,
        DigestFormatter formatter,
        INotificationRunRepository notificationRunRepository,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        ILogger<WeeklyDigestJob> logger)
    {
        _buildWeeklyDigest = buildWeeklyDigest;
        _sendDigest = sendDigest;
        _formatter = formatter;
        _notificationRunRepository = notificationRunRepository;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("WeeklyDigestJob started");

        try
        {
            var result = await _buildWeeklyDigest.ExecuteAsync(context.CancellationToken);

            var periodStartLocal = result.PeriodStart.AtMidnight().ToDateTimeUnspecified();
            var periodEndLocal = result.PeriodEnd.At(NodaTime.LocalTime.MaxValue).ToDateTimeUnspecified();

            var exists = await _notificationRunRepository.ExistsAsync(
                DigestType.Weekly,
                periodStartLocal,
                periodEndLocal,
                result.TimeZoneId,
                context.CancellationToken);

            if (exists)
            {
                _logger.LogInformation("Weekly digest for {Start}-{End} already sent, skipping", result.PeriodStart, result.PeriodEnd);
                return;
            }

            var digestText = _formatter.FormatWeekly(result.PeriodStart, result.PeriodEnd, result.Items);
            await _sendDigest.ExecuteAsync(digestText, context.CancellationToken);

            var notificationRun = new NotificationRun(
                DigestType.Weekly,
                periodStartLocal,
                periodEndLocal,
                result.TimeZoneId,
                _clock.UtcNow);

            await _notificationRunRepository.AddAsync(notificationRun, context.CancellationToken);
            _logger.LogInformation("Weekly digest for {Start}-{End} sent successfully", result.PeriodStart, result.PeriodEnd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing WeeklyDigestJob");
            throw;
        }
    }
}