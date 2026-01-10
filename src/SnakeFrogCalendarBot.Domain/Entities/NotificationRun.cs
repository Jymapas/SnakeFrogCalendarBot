using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Domain.Entities;

public sealed class NotificationRun
{
    public int Id { get; private set; }
    public DigestType DigestType { get; private set; }
    public DateTime PeriodStartLocal { get; private set; }
    public DateTime PeriodEndLocal { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private NotificationRun()
    {
    }

    public NotificationRun(
        DigestType digestType,
        DateTime periodStartLocal,
        DateTime periodEndLocal,
        string timeZoneId,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new DomainException("TimeZoneId is required.");
        }

        DigestType = digestType;
        PeriodStartLocal = periodStartLocal;
        PeriodEndLocal = periodEndLocal;
        TimeZoneId = timeZoneId.Trim();
        CreatedAtUtc = createdAtUtc;
    }
}