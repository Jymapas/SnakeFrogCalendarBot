using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using AppClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Tests.Notifications;

public sealed class PeriodCalculationTests
{
    private sealed class TestClock : AppClock
    {
        private readonly DateTime _utcNow;

        public TestClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;
    }

    private sealed class TestTimeZoneProvider : ITimeZoneProvider
    {
        private readonly string _timeZoneId;

        public TestTimeZoneProvider(string timeZoneId = "Europe/Moscow")
        {
            _timeZoneId = timeZoneId;
        }

        public string GetTimeZoneId() => _timeZoneId;
    }

    [Test]
    public void BuildWeeklyDigest_OnSunday_CalculatesNextWeekCorrectly()
    {
        var sunday = new DateTime(2026, 1, 11, 21, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(sunday);
        var timeZoneProvider = new TestTimeZoneProvider();
        var timeZone = DateTimeZoneProviders.Tzdb[timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(sunday).InZone(timeZone);
        var today = nowInZone.Date;

        var daysUntilMonday = ((int)IsoDayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var nextMonday = daysUntilMonday == 0 ? today.PlusDays(7) : today.PlusDays(daysUntilMonday);

        Assert.That(nextMonday.DayOfWeek, Is.EqualTo(IsoDayOfWeek.Monday));
        Assert.That(nextMonday, Is.EqualTo(new LocalDate(2026, 1, 12)));
    }

    [Test]
    public void BuildWeeklyDigest_OnMonday_CalculatesNextWeekCorrectly()
    {
        var monday = new DateTime(2026, 1, 12, 10, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(monday);
        var timeZoneProvider = new TestTimeZoneProvider();
        var timeZone = DateTimeZoneProviders.Tzdb[timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(monday).InZone(timeZone);
        var today = nowInZone.Date;

        var daysUntilMonday = ((int)IsoDayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        var nextMonday = daysUntilMonday == 0 ? today.PlusDays(7) : today.PlusDays(daysUntilMonday);

        Assert.That(nextMonday.DayOfWeek, Is.EqualTo(IsoDayOfWeek.Monday));
        Assert.That(nextMonday, Is.EqualTo(new LocalDate(2026, 1, 19)));
    }

    [Test]
    public void BuildMonthlyDigest_OnLastDayOfMonth_CalculatesNextMonthCorrectly()
    {
        var lastDay = new DateTime(2026, 1, 31, 18, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(lastDay);
        var timeZoneProvider = new TestTimeZoneProvider();
        var timeZone = DateTimeZoneProviders.Tzdb[timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(lastDay).InZone(timeZone);
        var today = nowInZone.Date;

        var nextMonth = today.PlusMonths(1);
        var periodStart = new LocalDate(nextMonth.Year, nextMonth.Month, 1);
        var lastDayOfMonth = CalendarSystem.Iso.GetDaysInMonth(nextMonth.Year, nextMonth.Month);
        var periodEnd = new LocalDate(nextMonth.Year, nextMonth.Month, lastDayOfMonth);

        Assert.That(periodStart, Is.EqualTo(new LocalDate(2026, 2, 1)));
        Assert.That(periodEnd, Is.EqualTo(new LocalDate(2026, 2, 28)));
    }

    [Test]
    public void BuildMonthlyDigest_OnLastDayOfFebruary_CalculatesNextMonthCorrectly()
    {
        var lastDayFeb = new DateTime(2026, 2, 28, 18, 0, 0, DateTimeKind.Utc);
        var clock = new TestClock(lastDayFeb);
        var timeZoneProvider = new TestTimeZoneProvider();
        var timeZone = DateTimeZoneProviders.Tzdb[timeZoneProvider.GetTimeZoneId()];
        var nowInZone = Instant.FromDateTimeUtc(lastDayFeb).InZone(timeZone);
        var today = nowInZone.Date;

        var nextMonth = today.PlusMonths(1);
        var periodStart = new LocalDate(nextMonth.Year, nextMonth.Month, 1);
        var lastDayOfMonth = CalendarSystem.Iso.GetDaysInMonth(nextMonth.Year, nextMonth.Month);
        var periodEnd = new LocalDate(nextMonth.Year, nextMonth.Month, lastDayOfMonth);

        Assert.That(periodStart, Is.EqualTo(new LocalDate(2026, 3, 1)));
        Assert.That(periodEnd, Is.EqualTo(new LocalDate(2026, 3, 31)));
    }
}