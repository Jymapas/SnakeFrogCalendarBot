using Microsoft.Extensions.Logging;
using NodaTime;
using Quartz;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using IClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class DailyDigestJob : IJob
{
    private readonly PublishDigest _publishDigest;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ILogger<DailyDigestJob> _logger;

    public DailyDigestJob(
        PublishDigest publishDigest,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        ILogger<DailyDigestJob> logger)
    {
        _publishDigest = publishDigest;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("DailyDigestJob started");

        try
        {
            var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
            var today = Instant.FromDateTimeUtc(_clock.UtcNow).InZone(timeZone).Date;
            await _publishDigest.PublishDailyAsync(today, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DailyDigestJob");
            throw;
        }
    }
}
