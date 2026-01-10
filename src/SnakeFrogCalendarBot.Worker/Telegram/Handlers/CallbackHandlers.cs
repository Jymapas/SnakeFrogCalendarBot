using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SnakeFrogCalendarBot.Worker.Telegram.Handlers;

public sealed class CallbackHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversationStateRepository _conversationRepository;
    private readonly IClock _clock;
    private readonly GetEventWithAttachment _getEventWithAttachment;
    private readonly ReplaceEventFile _replaceEventFile;

    public CallbackHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IClock clock,
        GetEventWithAttachment getEventWithAttachment,
        ReplaceEventFile replaceEventFile)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _clock = clock;
        _getEventWithAttachment = getEventWithAttachment;
        _replaceEventFile = replaceEventFile;
    }

    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.From?.Id is not { } userId || string.IsNullOrWhiteSpace(callbackQuery.Data))
        {
            return;
        }

        var data = callbackQuery.Data;

        if (data.StartsWith("event_attach:") || data.StartsWith("event_replace_file:"))
        {
            var isReplace = data.StartsWith("event_replace_file:");
            var eventIdStr = data.Contains(':') ? data.Split(':')[1] : null;
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                await _botClient.AnswerCallbackQueryAsync(
                    callbackQuery.Id,
                    "Ошибка: неверный идентификатор события",
                    cancellationToken: cancellationToken);
                return;
            }

            var eventWithAttachment = await _getEventWithAttachment.ExecuteAsync(eventId, cancellationToken);
            if (eventWithAttachment is null)
            {
                await _botClient.AnswerCallbackQueryAsync(
                    callbackQuery.Id,
                    "Событие не найдено",
                    cancellationToken: cancellationToken);
                return;
            }

            if (isReplace && eventWithAttachment.Attachments.Count == 0)
            {
                await _botClient.AnswerCallbackQueryAsync(
                    callbackQuery.Id,
                    "Нет файлов для замены",
                    cancellationToken: cancellationToken);
                return;
            }

            var now = _clock.UtcNow;
            var step = isReplace ? $"replace:{eventId}" : eventId.ToString();
            var state = new ConversationState(
                userId,
                ConversationNames.WaitingForEventFile,
                step,
                null,
                now);

            await _conversationRepository.UpsertAsync(state, cancellationToken);

            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                cancellationToken: cancellationToken);

            var messageText = isReplace
                ? "Отправьте файл для замены последнего прикрепленного файла"
                : eventWithAttachment.Attachments.Count > 0
                    ? $"Отправьте файл для добавления к событию (уже прикреплено файлов: {eventWithAttachment.Attachments.Count})"
                    : "Отправьте файл, который нужно прикрепить к событию";

            await _botClient.SendTextMessageAsync(
                callbackQuery.Message!.Chat.Id,
                messageText,
                cancellationToken: cancellationToken);
        }
    }
}