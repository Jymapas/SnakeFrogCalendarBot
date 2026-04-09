using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Dto;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class SendDigest
{
    private readonly ITelegramPublisher _telegramPublisher;
    private readonly ILogger<SendDigest> _logger;

    public SendDigest(ITelegramPublisher telegramPublisher, ILogger<SendDigest>? logger = null)
    {
        _telegramPublisher = telegramPublisher;
        _logger = logger ?? NullLogger<SendDigest>.Instance;
    }

    public async Task<int> ExecuteAsync(
        string digestText,
        IReadOnlyList<DigestAttachmentDto>? attachments,
        CancellationToken cancellationToken)
    {
        var messageId = await _telegramPublisher.SendMessageAsync(digestText, cancellationToken);

        if (attachments is null || attachments.Count == 0)
        {
            return messageId;
        }

        foreach (var attachment in attachments)
        {
            try
            {
                await _telegramPublisher.SendDocumentAsync(
                    attachment.TelegramFileId,
                    attachment.FileName,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // The digest post itself is already published; file failures should not break run idempotency.
                _logger.LogWarning(
                    ex,
                    "Failed to send attachment {FileName} for digest post; digest post remains published",
                    attachment.FileName);
            }
        }

        return messageId;
    }
}
