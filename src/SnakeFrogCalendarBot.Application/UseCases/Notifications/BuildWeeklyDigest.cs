using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class BuildWeeklyDigest
{
    private readonly DigestItemsProvider _digestItemsProvider;
    private readonly AppClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public BuildWeeklyDigest(
        DigestItemsProvider digestItemsProvider,
        AppClock clock,
        ITimeZoneProvider timeZoneProvider)
    {
        _digestItemsProvider = digestItemsProvider;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public async Task<WeeklyDigestResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var daysUntilMonday = ((int)IsoDayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var nextMonday = daysUntilMonday == 0 ? today.PlusDays(7) : today.PlusDays(daysUntilMonday);

        var periodStart = nextMonday;
        var periodEnd = nextMonday.PlusDays(6);

        var items = await _digestItemsProvider.BuildAsync(periodStart, periodEnd, timeZone.Id, cancellationToken);

        return new WeeklyDigestResult(periodStart, periodEnd, items, timeZone.Id);
    }
}

public sealed record WeeklyDigestResult(
    LocalDate PeriodStart,
    LocalDate PeriodEnd,
    IReadOnlyList<CalendarItemDto> Items,
    string TimeZoneId);
