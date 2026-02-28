using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class BuildDailyDigest
{
    private readonly DigestItemsProvider _digestItemsProvider;
    private readonly AppClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public BuildDailyDigest(
        DigestItemsProvider digestItemsProvider,
        AppClock clock,
        ITimeZoneProvider timeZoneProvider)
    {
        _digestItemsProvider = digestItemsProvider;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public async Task<DailyDigestResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;
        var items = await _digestItemsProvider.BuildAsync(today, today, timeZone.Id, cancellationToken);

        return new DailyDigestResult(today, items, timeZone.Id);
    }
}

public sealed record DailyDigestResult(
    LocalDate Date,
    IReadOnlyList<CalendarItemDto> Items,
    string TimeZoneId);
