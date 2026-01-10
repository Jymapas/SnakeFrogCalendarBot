using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram.Handlers;

public sealed class CallbackHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversationStateRepository _conversationRepository;
    private readonly IClock _clock;
    private readonly GetEventWithAttachment _getEventWithAttachment;
    private readonly ReplaceEventFile _replaceEventFile;
    private readonly IEventRepository _eventRepository;
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly DeleteEvent _deleteEvent;
    private readonly DeleteBirthday _deleteBirthday;

    public CallbackHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IClock clock,
        GetEventWithAttachment getEventWithAttachment,
        ReplaceEventFile replaceEventFile,
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository,
        DeleteEvent deleteEvent,
        DeleteBirthday deleteBirthday)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _clock = clock;
        _getEventWithAttachment = getEventWithAttachment;
        _replaceEventFile = replaceEventFile;
        _eventRepository = eventRepository;
        _birthdayRepository = birthdayRepository;
        _deleteEvent = deleteEvent;
        _deleteBirthday = deleteBirthday;
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
        else if (data.StartsWith("event_edit:"))
        {
            await HandleEventEditAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("event_edit_field:"))
        {
            await HandleEventEditFieldAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("event_delete:"))
        {
            await HandleEventDeleteAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("birthday_edit:"))
        {
            await HandleBirthdayEditAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("birthday_edit_field:"))
        {
            await HandleBirthdayEditFieldAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("birthday_delete:"))
        {
            await HandleBirthdayDeleteAsync(callbackQuery, cancellationToken);
        }
        else if (data == "delete_confirm_yes" || data.StartsWith("delete_confirm_yes:"))
        {
            await HandleDeleteConfirmationAsync(callbackQuery, cancellationToken);
        }
        else if (data == "delete_confirm_no" || data == "cancel")
        {
            await HandleCancelAsync(callbackQuery, cancellationToken);
        }
    }

    private async Task HandleCancelAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.From?.Id is not { } userId)
        {
            return;
        }

        await _conversationRepository.DeleteAsync(userId, cancellationToken);
        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);
        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            "Действие отменено",
            cancellationToken: cancellationToken);
    }

    private async Task HandleEventEditAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var eventIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(eventIdStr, out var eventId))
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = CreateEventEditKeyboard(eventId);
        var text = $"Редактирование события: {eventEntity.Title}\n\nВыберите поле для редактирования:";

        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleEventEditFieldAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var parts = callbackQuery.Data!.Split(':');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var eventId))
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var field = parts[2];
        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var now = _clock.UtcNow;
        var state = new ConversationState(
            callbackQuery.From!.Id,
            ConversationNames.EventEdit,
            $"{field}:{eventId}",
            null,
            now);

        await _conversationRepository.UpsertAsync(state, cancellationToken);

        var messageText = field switch
        {
            "title" => "Введите новое название события:",
            "description" => "Введите новое описание (или 'пропустить' для удаления):",
            "place" => "Введите новое место (или 'пропустить' для удаления):",
            "link" => "Введите новую ссылку (или 'пропустить' для удаления):",
            "date" => "Введите новую дату (например, 7 января 2026 или 2026-01-07):",
            "time" => "Введите новое время (HH:mm):",
            "isAllDay" => "Это событие на весь день? (да/нет):",
            _ => "Введите новое значение:"
        };

        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            messageText,
            cancellationToken: cancellationToken);
    }

    private async Task HandleEventDeleteAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var eventIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(eventIdStr, out var eventId))
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Да", $"delete_confirm_yes:event:{eventId}"),
                InlineKeyboardButton.WithCallbackData("Отмена", "delete_confirm_no")
            }
        });

        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            $"Вы действительно хотите удалить событие «{eventEntity.Title}»?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleBirthdayEditAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var birthdayIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(birthdayIdStr, out var birthdayId))
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор дня рождения",
                cancellationToken: cancellationToken);
            return;
        }

        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = CreateBirthdayEditKeyboard(birthdayId);
        var text = $"Редактирование дня рождения: {birthday.PersonName}\n\nВыберите поле для редактирования:";

        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            text,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleBirthdayEditFieldAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var parts = callbackQuery.Data!.Split(':');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var birthdayId))
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var field = parts[2];
        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var now = _clock.UtcNow;
        var state = new ConversationState(
            callbackQuery.From!.Id,
            ConversationNames.BirthdayEdit,
            $"{field}:{birthdayId}",
            null,
            now);

        await _conversationRepository.UpsertAsync(state, cancellationToken);

        var messageText = field switch
        {
            "personName" => "Введите новое имя:",
            "date" => "Введите новую дату (например, 7 января):",
            "birthYear" => "Введите новый год рождения (или 'пропустить' для удаления):",
            "contact" => "Введите новый контакт (или 'пропустить' для удаления):",
            _ => "Введите новое значение:"
        };

        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            messageText,
            cancellationToken: cancellationToken);
    }

    private async Task HandleBirthdayDeleteAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var birthdayIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(birthdayIdStr, out var birthdayId))
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор дня рождения",
                cancellationToken: cancellationToken);
            return;
        }

        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _botClient.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Да", $"delete_confirm_yes:birthday:{birthdayId}"),
                InlineKeyboardButton.WithCallbackData("Отмена", "delete_confirm_no")
            }
        });

        await _botClient.SendTextMessageAsync(
            callbackQuery.Message!.Chat.Id,
            $"Вы действительно хотите удалить день рождения «{birthday.PersonName}»?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleDeleteConfirmationAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQueryAsync(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = callbackQuery.Data!.Split(':');
        if (parts.Length < 3)
        {
            await _botClient.SendTextMessageAsync(
                callbackQuery.Message!.Chat.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var entityType = parts[1];
        if (!int.TryParse(parts[2], out var entityId))
        {
            await _botClient.SendTextMessageAsync(
                callbackQuery.Message!.Chat.Id,
                "Ошибка: неверный идентификатор",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            if (entityType == "event")
            {
                await _deleteEvent.ExecuteAsync(entityId, cancellationToken);
                await _botClient.SendTextMessageAsync(
                    callbackQuery.Message!.Chat.Id,
                    "Событие удалено",
                    cancellationToken: cancellationToken);
            }
            else if (entityType == "birthday")
            {
                await _deleteBirthday.ExecuteAsync(entityId, cancellationToken);
                await _botClient.SendTextMessageAsync(
                    callbackQuery.Message!.Chat.Id,
                    "День рождения удалён",
                    cancellationToken: cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            await _botClient.SendTextMessageAsync(
                callbackQuery.Message!.Chat.Id,
                $"Ошибка: {ex.Message}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            await _botClient.SendTextMessageAsync(
                callbackQuery.Message!.Chat.Id,
                "Произошла ошибка при удалении. Попробуйте позже.",
                cancellationToken: cancellationToken);
        }
    }

    private static InlineKeyboardMarkup CreateEventEditKeyboard(int eventId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Название", $"event_edit_field:{eventId}:title") },
            new[] { InlineKeyboardButton.WithCallbackData("🗓 Дата / время", $"event_edit_field:{eventId}:date") },
            new[] { InlineKeyboardButton.WithCallbackData("📝 Описание", $"event_edit_field:{eventId}:description") },
            new[] { InlineKeyboardButton.WithCallbackData("📍 Место", $"event_edit_field:{eventId}:place") },
            new[] { InlineKeyboardButton.WithCallbackData("🔗 Ссылка", $"event_edit_field:{eventId}:link") },
            new[] { InlineKeyboardButton.WithCallbackData("📎 Файл", $"event_attach:{eventId}") },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel") }
        });
    }

    private static InlineKeyboardMarkup CreateBirthdayEditKeyboard(int birthdayId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✏️ Имя", $"birthday_edit_field:{birthdayId}:personName") },
            new[] { InlineKeyboardButton.WithCallbackData("🎂 Дата", $"birthday_edit_field:{birthdayId}:date") },
            new[] { InlineKeyboardButton.WithCallbackData("📅 Год рождения", $"birthday_edit_field:{birthdayId}:birthYear") },
            new[] { InlineKeyboardButton.WithCallbackData("🔗 Контакт", $"birthday_edit_field:{birthdayId}:contact") },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel") }
        });
    }
}