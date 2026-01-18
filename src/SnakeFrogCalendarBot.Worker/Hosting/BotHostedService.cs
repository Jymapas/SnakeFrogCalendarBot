using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Worker.Telegram;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SnakeFrogCalendarBot.Worker.Hosting;

public sealed class BotHostedService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateDispatcher _updateDispatcher;
    private readonly ITelegramPublisher _telegramPublisher;
    private readonly ILogger<BotHostedService> _logger;
    private CancellationTokenSource? _cts;

    public BotHostedService(
        ITelegramBotClient botClient,
        UpdateDispatcher updateDispatcher,
        ITelegramPublisher telegramPublisher,
        ILogger<BotHostedService> logger)
    {
        _botClient = botClient;
        _updateDispatcher = updateDispatcher;
        _telegramPublisher = telegramPublisher;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Telegram long polling");

        try
        {
            var commands = BotCommands.AsBotCommands();
            await _botClient.SetMyCommands(
                commands,
                scope: new BotCommandScopeAllPrivateChats(),
                cancellationToken: cancellationToken);
            _logger.LogInformation("Bot commands menu set for private chats");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set bot commands menu");
        }

        try
        {
            await _telegramPublisher.SendMessageAsync("Бот был перезапущен", cancellationToken);
            _logger.LogInformation("Restart notification sent to channel");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send restart notification to channel");
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        _botClient.StartReceiving(
            _updateDispatcher.HandleUpdateAsync,
            _updateDispatcher.HandleErrorAsync,
            receiverOptions,
            _cts.Token);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram long polling");
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}
