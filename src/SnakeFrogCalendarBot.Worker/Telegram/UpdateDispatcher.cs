using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public sealed class UpdateDispatcher
{
    private readonly AccessGuard _accessGuard;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UpdateDispatcher> _logger;

    public UpdateDispatcher(
        AccessGuard accessGuard,
        IServiceScopeFactory scopeFactory,
        ILogger<UpdateDispatcher> logger)
    {
        _accessGuard = accessGuard;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        var userId = message.From?.Id;
        if (userId is null)
        {
            return;
        }

        if (!_accessGuard.IsAllowed(userId.Value))
        {
            await botClient.SendTextMessageAsync(
                message.Chat.Id,
                "У вас нет доступа к этому боту",
                cancellationToken: cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var commandHandlers = scope.ServiceProvider.GetRequiredService<Handlers.CommandHandlers>();
        var messageHandlers = scope.ServiceProvider.GetRequiredService<Handlers.MessageHandlers>();

        if (message.Text.StartsWith('/'))
        {
            await commandHandlers.HandleAsync(message, cancellationToken);
        }
        else
        {
            await messageHandlers.HandleAsync(message, cancellationToken);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}
