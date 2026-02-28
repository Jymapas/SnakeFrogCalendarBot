using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Domain.Entities;

public sealed class LatestDigestPost
{
    public int Id { get; private set; }
    public DigestType DigestType { get; private set; }
    public int NotificationRunId { get; private set; }
    public int TelegramMessageId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private LatestDigestPost()
    {
    }

    public LatestDigestPost(
        DigestType digestType,
        int notificationRunId,
        int telegramMessageId,
        DateTime updatedAtUtc)
    {
        Validate(notificationRunId, telegramMessageId);

        DigestType = digestType;
        NotificationRunId = notificationRunId;
        TelegramMessageId = telegramMessageId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Update(int notificationRunId, int telegramMessageId, DateTime updatedAtUtc)
    {
        Validate(notificationRunId, telegramMessageId);

        NotificationRunId = notificationRunId;
        TelegramMessageId = telegramMessageId;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void Validate(int notificationRunId, int telegramMessageId)
    {
        if (notificationRunId <= 0)
        {
            throw new DomainException("NotificationRunId must be a positive number.");
        }

        if (telegramMessageId <= 0)
        {
            throw new DomainException("TelegramMessageId must be a positive number.");
        }
    }
}
