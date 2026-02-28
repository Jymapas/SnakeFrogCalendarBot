using NodaTime;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class DigestPeriodCalculator
{
    public LocalDate CalculateDailyDate(LocalDate triggerDate)
    {
        return triggerDate;
    }

    public (LocalDate PeriodStart, LocalDate PeriodEnd) CalculateWeeklyPeriod(LocalDate triggerDate)
    {
        var daysUntilMonday = ((int)IsoDayOfWeek.Monday - (int)triggerDate.DayOfWeek + 7) % 7;
        var nextMonday = daysUntilMonday == 0 ? triggerDate.PlusDays(7) : triggerDate.PlusDays(daysUntilMonday);
        return (nextMonday, nextMonday.PlusDays(6));
    }

    public (LocalDate PeriodStart, LocalDate PeriodEnd) CalculateMonthlyPeriod(LocalDate triggerDate)
    {
        var nextMonth = triggerDate.PlusMonths(1);
        var periodStart = new LocalDate(nextMonth.Year, nextMonth.Month, 1);
        var lastDayOfMonth = CalendarSystem.Iso.GetDaysInMonth(nextMonth.Year, nextMonth.Month);
        var periodEnd = new LocalDate(nextMonth.Year, nextMonth.Month, lastDayOfMonth);
        return (periodStart, periodEnd);
    }
}
