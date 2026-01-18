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
    private readonly ILogger<TelegramPublisher> _logger;

    public TelegramPublisher(
        ITelegramBotClient botClient,
        string targetChat,
        ILogger<TelegramPublisher> logger)
    {
        _botClient = botClient;
        _targetChat = targetChat;
        _logger = logger;
    }

    public async Task SendMessageAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var chatId = new ChatId(_targetChat);
            await _botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
            _logger.LogInformation("Message sent to {TargetChat}", _targetChat);
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
}