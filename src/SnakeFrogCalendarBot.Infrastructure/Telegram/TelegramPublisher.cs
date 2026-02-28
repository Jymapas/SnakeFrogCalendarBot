using Microsoft.Extensions.Logging;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SnakeFrogCalendarBot.Infrastructure.Telegram;

public sealed class TelegramPublisher : ITelegramPublisher
{
    private readonly ITelegramBotClient _botClient;
    private readonly string _targetChat;
    private readonly IPinnedMessageCleanupRegistry _cleanupRegistry;
    private readonly ILogger<TelegramPublisher> _logger;

    public TelegramPublisher(
        ITelegramBotClient botClient,
        string targetChat,
        IPinnedMessageCleanupRegistry cleanupRegistry,
        ILogger<TelegramPublisher> logger)
    {
        _botClient = botClient;
        _targetChat = targetChat;
        _cleanupRegistry = cleanupRegistry;
        _logger = logger;
    }

    public async Task<int> SendMessageAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var chatId = new ChatId(_targetChat);
            var message = await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
            _logger.LogInformation("Message sent to {TargetChat}", _targetChat);
            return message.MessageId;
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (errorMessage.Contains("chat not found") || errorMessage.Contains("400"))
            {
                _logger.LogError(
                    ex,
                    "Failed to send message to {TargetChat}. " +
                    "Проверьте, что TELEGRAM_TARGET_CHAT указан правильно (формат: @channelname или -1001234567890). " +
                    "Убедитесь, что бот добавлен в канал как администратор.",
                    _targetChat);
            }
            else
            {
                _logger.LogError(ex, "Failed to send message to {TargetChat}", _targetChat);
            }
            throw;
        }
    }

    public async Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken)
    {
        try
        {
            var chatId = new ChatId(_targetChat);
            await _botClient.EditMessageText(chatId, messageId, text, cancellationToken: cancellationToken);
            _logger.LogInformation("Message {MessageId} edited in {TargetChat}", messageId, _targetChat);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Message {MessageId} in {TargetChat} is already up to date", messageId, _targetChat);
                return;
            }

            _logger.LogError(ex, "Failed to edit message {MessageId} in {TargetChat}", messageId, _targetChat);
            throw;
        }
    }

    public async Task DeleteMessageAsync(int messageId, CancellationToken cancellationToken)
    {
        try
        {
            var chatId = new ChatId(_targetChat);
            await _botClient.DeleteMessage(chatId, messageId, cancellationToken);
            _logger.LogInformation("Message {MessageId} deleted in {TargetChat}", messageId, _targetChat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message {MessageId} in {TargetChat}", messageId, _targetChat);
            throw;
        }
    }

    public async Task PinMessageAsync(int messageId, bool disableNotification, CancellationToken cancellationToken)
    {
        try
        {
            var chatId = new ChatId(_targetChat);
            await _botClient.PinChatMessage(
                chatId,
                messageId,
                disableNotification: disableNotification,
                cancellationToken: cancellationToken);
            _logger.LogInformation(
                "Message {MessageId} pinned in {TargetChat} with disableNotification={DisableNotification}",
                messageId,
                _targetChat,
                disableNotification);
            _cleanupRegistry.RegisterPinnedMessage(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pin message {MessageId} in {TargetChat}", messageId, _targetChat);
            throw;
        }
    }

    public async Task UnpinMessageAsync(int messageId, CancellationToken cancellationToken)
    {
        try
        {
            var chatId = new ChatId(_targetChat);
            await _botClient.UnpinChatMessage(
                chatId,
                messageId,
                cancellationToken: cancellationToken);
            _logger.LogInformation("Message {MessageId} unpinned in {TargetChat}", messageId, _targetChat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unpin message {MessageId} in {TargetChat}", messageId, _targetChat);
            throw;
        }
    }
}
