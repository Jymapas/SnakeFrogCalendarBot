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
            _logger.LogInformation("Digest sent to {TargetChat}", _targetChat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send digest to {TargetChat}", _targetChat);
            throw;
        }
    }
}