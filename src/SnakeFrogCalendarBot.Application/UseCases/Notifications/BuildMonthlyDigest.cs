using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Dto;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class BuildMonthlyDigest
{
    private readonly DigestItemsProvider _digestItemsProvider;
    private readonly AppClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;

    public BuildMonthlyDigest(
        DigestItemsProvider digestItemsProvider,
        AppClock clock,
        ITimeZoneProvider timeZoneProvider)
    {
        _digestItemsProvider = digestItemsProvider;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public async Task<MonthlyDigestResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var nextMonth = today.PlusMonths(1);
        var periodStart = new LocalDate(nextMonth.Year, nextMonth.Month, 1);
        var lastDayOfMonth = CalendarSystem.Iso.GetDaysInMonth(nextMonth.Year, nextMonth.Month);
        var periodEnd = new LocalDate(nextMonth.Year, nextMonth.Month, lastDayOfMonth);

        var items = await _digestItemsProvider.BuildAsync(periodStart, periodEnd, timeZone.Id, cancellationToken);

        return new MonthlyDigestResult(periodStart, periodEnd, items, timeZone.Id);
    }
}

public sealed record MonthlyDigestResult(
    LocalDate PeriodStart,
    LocalDate PeriodEnd,
    IReadOnlyList<CalendarItemDto> Items,
    string TimeZoneId);
