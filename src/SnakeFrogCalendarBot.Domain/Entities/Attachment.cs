using SnakeFrogCalendarBot.Domain.Exceptions;

namespace SnakeFrogCalendarBot.Domain.Entities;

public sealed class Attachment
{
    public int Id { get; private set; }
    public int EventId { get; private set; }
    public string TelegramFileId { get; private set; } = string.Empty;
    public string TelegramFileUniqueId { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string? MimeType { get; private set; }
    public long? Size { get; private set; }
    public int Version { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    private Attachment()
    {
    }

    public Attachment(
        int eventId,
        string telegramFileId,
        string telegramFileUniqueId,
        string fileName,
        string? mimeType,
        long? size,
        int version,
        bool isCurrent,
        DateTime uploadedAtUtc)
    {
        if (eventId <= 0)
        {
            throw new DomainException("EventId must be positive.");
        }

        if (string.IsNullOrWhiteSpace(telegramFileId))
        {
            throw new DomainException("TelegramFileId is required.");
        }

        if (string.IsNullOrWhiteSpace(telegramFileUniqueId))
        {
            throw new DomainException("TelegramFileUniqueId is required.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("FileName is required.");
        }

        if (version < 1)
        {
            throw new DomainException("Version must be at least 1.");
        }

        EventId = eventId;
        TelegramFileId = telegramFileId.Trim();
        TelegramFileUniqueId = telegramFileUniqueId.Trim();
        FileName = fileName.Trim();
        MimeType = string.IsNullOrWhiteSpace(mimeType) ? null : mimeType.Trim();
        Size = size;
        Version = version;
        IsCurrent = isCurrent;
        UploadedAtUtc = uploadedAtUtc;
    }

    public void MarkAsNotCurrent()
    {
        IsCurrent = false;
    }
}