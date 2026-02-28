using Microsoft.Extensions.Logging;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Worker.Config;
using Telegram.Bot.Types;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public sealed class PinnedServiceMessageCleaner
{
    private readonly ITelegramPublisher _telegramPublisher;
    private readonly IPinnedMessageCleanupRegistry _cleanupRegistry;
    private readonly AppOptions _appOptions;
    private readonly ILogger<PinnedServiceMessageCleaner> _logger;

    public PinnedServiceMessageCleaner(
        ITelegramPublisher telegramPublisher,
        IPinnedMessageCleanupRegistry cleanupRegistry,
        AppOptions appOptions,
        ILogger<PinnedServiceMessageCleaner> logger)
    {
        _telegramPublisher = telegramPublisher;
        _cleanupRegistry = cleanupRegistry;
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task TryDeleteAsync(Message channelPost, CancellationToken cancellationToken)
    {
        if (channelPost.Chat is null)
        {
            return;
        }

        if (!IsTargetChat(channelPost.Chat))
        {
            return;
        }

        var pinnedMessageId = channelPost.PinnedMessage?.MessageId;
        if (!pinnedMessageId.HasValue)
        {
            return;
        }

        if (!_cleanupRegistry.TryConsumePinnedMessage(pinnedMessageId.Value))
        {
            return;
        }

        try
        {
            await _telegramPublisher.DeleteMessageAsync(channelPost.MessageId, cancellationToken);
            _logger.LogInformation(
                "Deleted service pin message {ServiceMessageId} for pinned post {PinnedMessageId}",
                channelPost.MessageId,
                pinnedMessageId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete service pin message {ServiceMessageId} for pinned post {PinnedMessageId}",
                channelPost.MessageId,
                pinnedMessageId.Value);
        }
    }

    private bool IsTargetChat(Chat chat)
    {
        var targetChat = _appOptions.TelegramTargetChat.Trim();
        if (targetChat.StartsWith("@", StringComparison.Ordinal))
        {
            var expectedUsername = targetChat[1..];
            return !string.IsNullOrWhiteSpace(chat.Username)
                && string.Equals(chat.Username, expectedUsername, StringComparison.OrdinalIgnoreCase);
        }

        return long.TryParse(targetChat, out var targetChatId) && chat.Id == targetChatId;
    }
}
