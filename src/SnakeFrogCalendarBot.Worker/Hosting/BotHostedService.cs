using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SnakeFrogCalendarBot.Worker.Telegram;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace SnakeFrogCalendarBot.Worker.Hosting;

public sealed class BotHostedService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateDispatcher _updateDispatcher;
    private readonly ILogger<BotHostedService> _logger;
    private CancellationTokenSource? _cts;

    public BotHostedService(
        ITelegramBotClient botClient,
        UpdateDispatcher updateDispatcher,
        ILogger<BotHostedService> logger)
    {
        _botClient = botClient;
        _updateDispatcher = updateDispatcher;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Telegram long polling");

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

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram long polling");
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}
