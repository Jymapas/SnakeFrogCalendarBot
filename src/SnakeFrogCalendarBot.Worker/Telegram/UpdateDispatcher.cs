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
        if (update.CallbackQuery is { } callbackQuery)
        {
            await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
            return;
        }

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
            await botClient.SendMessage(
                message.Chat.Id,
                "У вас нет доступа к этому боту",
                cancellationToken: cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var commandHandlers = scope.ServiceProvider.GetRequiredService<Handlers.CommandHandlers>();
        var messageHandlers = scope.ServiceProvider.GetRequiredService<Handlers.MessageHandlers>();

        if (!string.IsNullOrWhiteSpace(message.Text) && message.Text.StartsWith('/'))
        {
            await commandHandlers.HandleAsync(message, cancellationToken);
        }
        else
        {
            await messageHandlers.HandleAsync(message, cancellationToken);
        }
    }

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient botClient,
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        var userId = callbackQuery.From?.Id;
        if (userId is null)
        {
            return;
        }

        if (!_accessGuard.IsAllowed(userId.Value))
        {
            await botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "У вас нет доступа к этому боту",
                cancellationToken: cancellationToken);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var callbackHandlers = scope.ServiceProvider.GetRequiredService<Handlers.CallbackHandlers>();
        await callbackHandlers.HandleAsync(callbackQuery, cancellationToken);
    }

    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception.Message;
        if (errorMessage.Contains("409") || errorMessage.Contains("Conflict"))
        {
            _logger.LogWarning(
                exception,
                "Telegram Bot API error 409: Другой экземпляр бота уже запущен. Убедитесь, что запущен только один экземпляр приложения. " +
                "Остановите другие экземпляры или подождите несколько секунд перед повторным запуском.");
        }
        else
        {
            _logger.LogError(exception, "Telegram polling error");
        }
        return Task.CompletedTask;
    }
}
