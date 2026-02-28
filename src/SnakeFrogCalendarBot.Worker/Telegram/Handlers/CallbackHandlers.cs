using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Worker.Config;
using SnakeFrogCalendarBot.Worker.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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
    private readonly string _botToken;
    private readonly HttpClient _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ListBirthdays _listBirthdays;
    private readonly BirthdayListFormatter _birthdayFormatter;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly ListUpcomingItems _listUpcomingItems;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly EventListFormatter _eventFormatter;

    public CallbackHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IClock clock,
        GetEventWithAttachment getEventWithAttachment,
        ReplaceEventFile replaceEventFile,
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository,
        DeleteEvent deleteEvent,
        DeleteBirthday deleteBirthday,
        AppOptions appOptions,
        IServiceProvider serviceProvider,
        ListBirthdays listBirthdays,
        BirthdayListFormatter birthdayFormatter,
        ITimeZoneProvider timeZoneProvider,
        ListUpcomingItems listUpcomingItems,
        IAttachmentRepository attachmentRepository,
        EventListFormatter eventFormatter)
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
        _botToken = appOptions.TelegramBotToken;
        _httpClient = new HttpClient();
        _serviceProvider = serviceProvider;
        _listBirthdays = listBirthdays;
        _birthdayFormatter = birthdayFormatter;
        _timeZoneProvider = timeZoneProvider;
        _listUpcomingItems = listUpcomingItems;
        _attachmentRepository = attachmentRepository;
        _eventFormatter = eventFormatter;
    }

    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.From?.Id is not { } userId || string.IsNullOrWhiteSpace(callbackQuery.Data))
        {
            return;
        }

        var data = callbackQuery.Data;

        if (data.StartsWith("menu:"))
        {
            await HandleMenuCallbackAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("cmd:"))
        {
            await HandleCommandCallbackAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("skip:"))
        {
            await HandleSkipCallbackAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("birthday_list_month:"))
        {
            await HandleBirthdayListMonthAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("birthday_edit_month:"))
        {
            await HandleBirthdayEditMonthAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("birthday_edit_month_page:"))
        {
            await HandleBirthdayEditMonthPageAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_edit_month:"))
        {
            await HandleEventEditMonthAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_edit_month_page:"))
        {
            await HandleEventEditMonthPageAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_list_month:"))
        {
            await HandleEventListMonthAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_list_month_page:"))
        {
            await HandleEventListMonthPageAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_view_week:"))
        {
            await HandleEventViewWeekAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_view_month:"))
        {
            await HandleEventViewMonthAsync(callbackQuery, data, cancellationToken);
            return;
        }

        if (data.StartsWith("event_attach_done:"))
        {
            var eventIdStr = data.Split(':', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Ошибка: неверный идентификатор события",
                    cancellationToken: cancellationToken);
                return;
            }

            var state = await _conversationRepository.GetByUserIdAsync(userId, cancellationToken);
            if (state is null || !string.Equals(state.ConversationName, ConversationNames.WaitingForEventFile, StringComparison.OrdinalIgnoreCase))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Нет активного добавления файлов",
                    cancellationToken: cancellationToken);
                return;
            }

            if (state.Step.StartsWith("replace:", StringComparison.OrdinalIgnoreCase))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Сейчас активна замена файла. Отправьте файл или /cancel",
                    cancellationToken: cancellationToken);
                return;
            }

            if (!string.Equals(state.Step, eventId.ToString(), StringComparison.Ordinal))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Добавление файлов уже переключено на другое событие",
                    cancellationToken: cancellationToken);
                return;
            }

            await _conversationRepository.DeleteAsync(userId, cancellationToken);
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                cancellationToken: cancellationToken);
            await _botClient.SendMessage(
                callbackQuery.Message!.Chat.Id,
                "Готово. Все файлы прикреплены.",
                cancellationToken: cancellationToken);
            return;
        }

        if (data.StartsWith("event_download_file:"))
        {
            var parts = data.Split(':', StringSplitOptions.RemoveEmptyEntries);
            var eventIdStr = parts.Length > 1 ? parts[1] : null;
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Ошибка: неверный идентификатор события",
                    cancellationToken: cancellationToken);
                return;
            }

            int? attachmentId = null;
            if (parts.Length > 2 && int.TryParse(parts[2], out var parsedAttachmentId))
            {
                attachmentId = parsedAttachmentId;
            }

            var eventWithAttachment = await _getEventWithAttachment.ExecuteAsync(eventId, cancellationToken);
            if (eventWithAttachment is null)
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Событие не найдено",
                    cancellationToken: cancellationToken);
                return;
            }

            IReadOnlyList<Domain.Entities.Attachment> attachmentsToSend;
            if (attachmentId.HasValue)
            {
                var attachment = eventWithAttachment.Attachments.FirstOrDefault(a => a.Id == attachmentId.Value);
                if (attachment is null)
                {
                    await _botClient.AnswerCallbackQuery(
                        callbackQuery.Id,
                        "Файл не найден",
                        cancellationToken: cancellationToken);
                    return;
                }

                attachmentsToSend = new[] { attachment };
            }
            else
            {
                var currentAttachments = eventWithAttachment.Attachments.Where(a => a.IsCurrent).ToList();
                attachmentsToSend = currentAttachments.Count > 0
                    ? currentAttachments
                    : eventWithAttachment.Attachments.ToList();
            }

            if (attachmentsToSend.Count == 0)
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Файл не найден",
                    cancellationToken: cancellationToken);
                return;
            }

            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                cancellationToken: cancellationToken);

            var chatId = callbackQuery.Message!.Chat.Id;
            var url = $"https://api.telegram.org/bot{_botToken}/sendDocument";
            var failedFiles = new List<string>();

            foreach (var attachment in attachmentsToSend)
            {
                try
                {
                    using var formData = new MultipartFormDataContent();
                    formData.Add(new StringContent(chatId.ToString()), "chat_id");
                    formData.Add(new StringContent(attachment.TelegramFileId), "document");
                    formData.Add(new StringContent($"Файл: {attachment.FileName}"), "caption");

                    var response = await _httpClient.PostAsync(url, formData, cancellationToken);
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        failedFiles.Add($"{attachment.FileName} ({responseContent})");
                    }
                }
                catch (Exception ex)
                {
                    failedFiles.Add($"{attachment.FileName} ({ex.Message})");
                }
            }

            if (failedFiles.Count > 0)
            {
                await _botClient.SendMessage(
                    callbackQuery.Message!.Chat.Id,
                    $"Ошибка при отправке файлов:\n{string.Join("\n", failedFiles)}",
                    cancellationToken: cancellationToken);
            }
        }
        else if (data.StartsWith("event_attach:") || data.StartsWith("event_replace_file:"))
        {
            var isReplace = data.StartsWith("event_replace_file:");
            var eventIdStr = data.Contains(':') ? data.Split(':')[1] : null;
            if (!int.TryParse(eventIdStr, out var eventId))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Ошибка: неверный идентификатор события",
                    cancellationToken: cancellationToken);
                return;
            }

            var eventWithAttachment = await _getEventWithAttachment.ExecuteAsync(eventId, cancellationToken);
            if (eventWithAttachment is null)
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Событие не найдено",
                    cancellationToken: cancellationToken);
                return;
            }

            if (isReplace && eventWithAttachment.Attachments.Count == 0)
            {
                await _botClient.AnswerCallbackQuery(
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

            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                cancellationToken: cancellationToken);

            var messageText = isReplace
                ? "Отправьте файл для замены последнего прикрепленного файла"
                : eventWithAttachment.Attachments.Count > 0
                    ? $"Отправьте файл для добавления к событию (уже прикреплено файлов: {eventWithAttachment.Attachments.Count}). Можно отправить несколько, затем нажмите «Готово»."
                    : "Отправьте файл, который нужно прикрепить к событию. Можно отправить несколько, затем нажмите «Готово».";

            await _botClient.SendMessage(
                callbackQuery.Message!.Chat.Id,
                messageText,
                replyMarkup: isReplace
                    ? new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel") }
                    })
                    : new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("✅ Готово", $"event_attach_done:{eventId}"),
                            InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                        }
                    }),
                cancellationToken: cancellationToken);
        }
        else if (data.StartsWith("event_view:"))
        {
            await HandleEventViewAsync(callbackQuery, cancellationToken);
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
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);
        await _botClient.SendMessage(
            callbackQuery.Message!.Chat.Id,
            "Действие отменено",
            cancellationToken: cancellationToken);
    }

    private async Task HandleEventViewAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var eventIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(eventIdStr, out var eventId))
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        var eventWithAttachment = await _getEventWithAttachment.ExecuteAsync(eventId, cancellationToken);
        var attachments = eventWithAttachment?.Attachments.ToList() ?? new List<Domain.Entities.Attachment>();
        var currentAttachments = attachments.Where(a => a.IsCurrent).ToList();
        var attachmentsForDisplay = currentAttachments.Count > 0 ? currentAttachments : attachments;

        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var text = FormatEventDetails(eventEntity, attachmentsForDisplay);
        var buttons = new List<List<InlineKeyboardButton>>();

        if (attachmentsForDisplay.Count > 0)
        {
            if (attachmentsForDisplay.Count > 1)
            {
                buttons.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"📥 Скачать все файлы ({attachmentsForDisplay.Count})", $"event_download_file:{eventId}")
                });
            }

            foreach (var attachment in attachmentsForDisplay)
            {
                buttons.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData($"📎 {attachment.FileName}", $"event_download_file:{eventId}:{attachment.Id}")
                });
            }
        }

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"event_edit:{eventId}")
        });

        await _botClient.SendMessage(
            callbackQuery.Message!.Chat.Id,
            text,
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: cancellationToken);
    }

    private string FormatEventDetails(Domain.Entities.Event eventEntity, IReadOnlyList<Domain.Entities.Attachment> attachments)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"📅 {eventEntity.Title}");
        builder.AppendLine();

        var timeZone = NodaTime.DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var now = _clock.UtcNow;
        var nowInZone = NodaTime.Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;
        var culture = CultureInfo.GetCultureInfo("ru-RU");

        if (eventEntity.Kind == Domain.Enums.EventKind.OneOff && eventEntity.OccursAtUtc.HasValue)
        {
            var instant = NodaTime.Instant.FromDateTimeUtc(eventEntity.OccursAtUtc.Value.UtcDateTime);
            var zonedDateTime = instant.InZone(timeZone);
            var localDateTime = zonedDateTime.LocalDateTime;

            if (eventEntity.IsAllDay)
            {
                builder.AppendLine($"📆 Дата: {localDateTime.Date.ToString("d MMMM yyyy", culture)}");
            }
            else
            {
                builder.AppendLine($"📆 Дата и время: {localDateTime.Date.ToString("d MMMM yyyy", culture)} {localDateTime.TimeOfDay.ToString("HH:mm", culture)}");
            }
        }
        else if (eventEntity.Kind == Domain.Enums.EventKind.Yearly && eventEntity.Month.HasValue && eventEntity.Day.HasValue)
        {
            var date = new NodaTime.LocalDate(2000, eventEntity.Month.Value, eventEntity.Day.Value);
            builder.Append("📆 Дата: ");
            builder.Append(date.ToString("d MMMM", culture));

            if (!eventEntity.IsAllDay && eventEntity.TimeOfDay.HasValue)
            {
                var time = NodaTime.LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks);
                builder.Append($" {time.ToString("HH:mm", CultureInfo.InvariantCulture)}");
            }

            builder.AppendLine(" (ежегодно)");
        }

        if (!string.IsNullOrWhiteSpace(eventEntity.Description))
        {
            builder.AppendLine();
            builder.AppendLine($"📝 Описание: {eventEntity.Description}");
        }

        if (!string.IsNullOrWhiteSpace(eventEntity.Place))
        {
            builder.AppendLine();
            builder.AppendLine($"📍 Место: {eventEntity.Place}");
        }

        if (!string.IsNullOrWhiteSpace(eventEntity.Link))
        {
            builder.AppendLine();
            builder.AppendLine($"🔗 Ссылка: {eventEntity.Link}");
        }

        if (attachments.Count == 1)
        {
            builder.AppendLine();
            builder.AppendLine($"📎 Файл: {attachments[0].FileName}");
        }
        else if (attachments.Count > 1)
        {
            builder.AppendLine();
            builder.AppendLine("📎 Файлы:");
            foreach (var attachment in attachments)
            {
                builder.AppendLine($"- {attachment.FileName}");
            }
        }

        return builder.ToString();
    }

    private async Task HandleEventEditAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var eventIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(eventIdStr, out var eventId))
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = CreateEventEditKeyboard(eventId);
        var text = $"Редактирование события: {eventEntity.Title}\n\nВыберите поле для редактирования:";

        await _botClient.SendMessage(
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
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var field = parts[2];
        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQuery(
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

        InlineKeyboardMarkup? keyboard = null;
        if (field is "description" or "place" or "link")
        {
            keyboard = CreateSkipKeyboardForEdit(ConversationNames.EventEdit, $"{field}:{eventId}");
        }

        await _botClient.SendMessage(
            callbackQuery.Message!.Chat.Id,
            messageText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleEventDeleteAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var eventIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(eventIdStr, out var eventId))
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQuery(
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

        await _botClient.SendMessage(
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
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор дня рождения",
                cancellationToken: cancellationToken);
            return;
        }

        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var keyboard = CreateBirthdayEditKeyboard(birthdayId);
        var text = $"Редактирование дня рождения: {birthday.PersonName}\n\nВыберите поле для редактирования:";

        await _botClient.SendMessage(
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
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var field = parts[2];
        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQuery(
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

        InlineKeyboardMarkup? keyboard = null;
        if (field is "birthYear" or "contact")
        {
            keyboard = CreateSkipKeyboardForEdit(ConversationNames.BirthdayEdit, $"{field}:{birthdayId}");
        }

        await _botClient.SendMessage(
            callbackQuery.Message!.Chat.Id,
            messageText,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleBirthdayDeleteAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var birthdayIdStr = callbackQuery.Data!.Split(':')[1];
        if (!int.TryParse(birthdayIdStr, out var birthdayId))
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "Ошибка: неверный идентификатор дня рождения",
                cancellationToken: cancellationToken);
            return;
        }

        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.AnswerCallbackQuery(
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

        await _botClient.SendMessage(
            callbackQuery.Message!.Chat.Id,
            $"Вы действительно хотите удалить день рождения «{birthday.PersonName}»?",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleDeleteConfirmationAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = callbackQuery.Data!.Split(':');
        if (parts.Length < 3)
        {
            await _botClient.SendMessage(
                callbackQuery.Message!.Chat.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var entityType = parts[1];
        if (!int.TryParse(parts[2], out var entityId))
        {
            await _botClient.SendMessage(
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
                await _botClient.SendMessage(
                    callbackQuery.Message!.Chat.Id,
                    "Событие удалено",
                    cancellationToken: cancellationToken);
            }
            else if (entityType == "birthday")
            {
                await _deleteBirthday.ExecuteAsync(entityId, cancellationToken);
                await _botClient.SendMessage(
                    callbackQuery.Message!.Chat.Id,
                    "День рождения удалён",
                    cancellationToken: cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            await _botClient.SendMessage(
                callbackQuery.Message!.Chat.Id,
                $"Ошибка: {ex.Message}",
                cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            await _botClient.SendMessage(
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

    private static InlineKeyboardMarkup CreateSkipKeyboardForEdit(string conversationName, string step)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ Пропустить", $"skip:{conversationName}:{step}")
            }
        });
    }

    private async Task HandleMenuCallbackAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var menuType = data.Split(':')[1];
        InlineKeyboardMarkup keyboard;
        string text;

        switch (menuType)
        {
            case "main":
                keyboard = InlineKeyboards.MainMenu();
                text = "Выберите действие:";
                break;
            case "events":
                keyboard = InlineKeyboards.EventsMenu();
                text = "Действия со событиями:";
                break;
            case "birthdays":
                keyboard = InlineKeyboards.BirthdaysMenu();
                text = "Действия с днями рождения:";
                break;
            default:
                return;
        }

        if (callbackQuery.Message is not null)
        {
            await _botClient.EditMessageText(
                callbackQuery.Message.Chat.Id,
                callbackQuery.Message.MessageId,
                text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                callbackQuery.From!.Id,
                text,
                replyMarkup: keyboard,
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleCommandCallbackAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var commandName = data.Split(':')[1];
        var command = commandName switch
        {
            "event_add" => BotCommands.EventAdd,
            "event_list" => BotCommands.EventList,
            "event_edit" => BotCommands.EventEdit,
            "event_delete" => BotCommands.EventDelete,
            "birthday_add" => BotCommands.BirthdayAdd,
            "birthday_list" => BotCommands.BirthdayList,
            "birthday_edit" => BotCommands.BirthdayEdit,
            "birthday_delete" => BotCommands.BirthdayDelete,
            _ => null
        };

        if (command is null)
        {
            return;
        }

        var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id;
        var virtualMessage = new Message
        {
            From = callbackQuery.From,
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = chatId, Type = ChatType.Private },
            Text = command
        };

        using var scope = _serviceProvider.CreateScope();
        var commandHandlers = scope.ServiceProvider.GetRequiredService<CommandHandlers>();
        await commandHandlers.HandleAsync(virtualMessage, cancellationToken);
    }

    private async Task HandleSkipCallbackAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 3)
        {
            return;
        }

        var conversationName = parts[1];
        var step = parts[2];

        var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id;
        var virtualMessage = new Message
        {
            From = callbackQuery.From,
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = chatId, Type = ChatType.Private },
            Text = "пропустить"
        };

        var state = await _conversationRepository.GetByUserIdAsync(callbackQuery.From!.Id, cancellationToken);
        if (state is null || state.ConversationName != conversationName || state.Step != step)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var messageHandlers = scope.ServiceProvider.GetRequiredService<MessageHandlers>();
        await messageHandlers.HandleAsync(virtualMessage, cancellationToken);
    }

    private async Task HandleBirthdayListMonthAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный номер месяца",
                cancellationToken: cancellationToken);
            return;
        }

        // Получаем все дни рождения и фильтруем по месяцу
        var allBirthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        var monthBirthdays = allBirthdays
            .Where(b => b.Month == month)
            .OrderBy(b => b.Day)
            .ToList();

        var monthName = CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetMonthName(month);
        var text = monthBirthdays.Count == 0
            ? $"Дней рождения в {monthName} нет"
            : $"Дни рождения в {monthName}:\n\n{_birthdayFormatter.Format(monthBirthdays)}";

        await _botClient.SendMessage(
            callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
            text,
            cancellationToken: cancellationToken);
    }

    private async Task HandleBirthdayEditMonthAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный номер месяца",
                cancellationToken: cancellationToken);
            return;
        }

        await SendBirthdayEditMonthPageAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, month, 0, null, cancellationToken);
    }

    private async Task HandleBirthdayEditMonthPageAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12 ||
            !int.TryParse(parts[2], out var page) || page < 0)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var messageId = callbackQuery.Message?.MessageId;
        await SendBirthdayEditMonthPageAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, month, page, messageId, cancellationToken);
    }

    private async Task SendBirthdayEditMonthPageAsync(long chatId, int month, int page, int? messageId, CancellationToken cancellationToken)
    {
        var allBirthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        var monthBirthdays = allBirthdays
            .Where(b => b.Month == month)
            .OrderBy(b => b.Day)
            .ThenBy(b => b.PersonName)
            .ToList();

        var monthName = CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetMonthName(month);

        if (monthBirthdays.Count == 0)
        {
            await _botClient.SendMessage(
                chatId,
                $"Дней рождения в {monthName} нет",
                cancellationToken: cancellationToken);
            return;
        }

        const int itemsPerPage = 10;
        var totalPages = (monthBirthdays.Count + itemsPerPage - 1) / itemsPerPage;
        var currentPage = Math.Min(page, totalPages - 1);
        var startIndex = currentPage * itemsPerPage;
        var endIndex = Math.Min(startIndex + itemsPerPage, monthBirthdays.Count);
        var pageBirthdays = monthBirthdays.Skip(startIndex).Take(endIndex - startIndex).ToList();

        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var birthday in pageBirthdays)
        {
            var dayText = $"{birthday.Day:D2}.{birthday.Month:D2}";
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    $"✏️ {dayText} {birthday.PersonName}",
                    $"birthday_edit:{birthday.Id}")
            });
        }

        if (totalPages > 1)
        {
            var navigationRow = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData(
                    "◀️ Назад",
                    $"birthday_edit_month_page:{month}:{currentPage - 1}"));
            }
            if (currentPage < totalPages - 1)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData(
                    "Вперёд ▶️",
                    $"birthday_edit_month_page:{month}:{currentPage + 1}"));
            }
            if (navigationRow.Count > 0)
            {
                buttons.Add(navigationRow);
            }
        }

        var text = $"Выберите день рождения для редактирования ({monthName}, страница {currentPage + 1} из {totalPages}):";

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                chatId,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleEventEditMonthAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный номер месяца",
                cancellationToken: cancellationToken);
            return;
        }

        await SendEventEditMonthPageAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, month, 0, null, cancellationToken);
    }

    private async Task HandleEventEditMonthPageAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12 ||
            !int.TryParse(parts[2], out var page) || page < 0)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var messageId = callbackQuery.Message?.MessageId;
        await SendEventEditMonthPageAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, month, page, messageId, cancellationToken);
    }

    private async Task SendEventEditMonthPageAsync(long chatId, int month, int page, int? messageId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = NodaTime.DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = NodaTime.Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var allUpcomingEvents = await _eventRepository.ListUpcomingForEditAsync(cancellationToken);
        
        var monthEvents = allUpcomingEvents
            .Where(e =>
            {
                var eventMonth = 0;

                if (e.Kind == Domain.Enums.EventKind.OneOff && e.OccursAtUtc.HasValue)
                {
                    var eventInstant = NodaTime.Instant.FromDateTimeUtc(e.OccursAtUtc.Value.UtcDateTime);
                    var eventInZone = eventInstant.InZone(timeZone);
                    eventMonth = eventInZone.Month;
                }
                else if (e.Kind == Domain.Enums.EventKind.Yearly && e.Month.HasValue && e.Day.HasValue)
                {
                    var thisYear = new NodaTime.LocalDate(today.Year, e.Month.Value, e.Day.Value);
                    var nextOccurrence = thisYear >= today ? thisYear : thisYear.PlusYears(1);
                    eventMonth = nextOccurrence.Month;
                }

                return eventMonth == month;
            })
            .OrderBy(e =>
            {
                if (e.Kind == Domain.Enums.EventKind.OneOff && e.OccursAtUtc.HasValue)
                {
                    return e.OccursAtUtc.Value.UtcDateTime;
                }

                var thisYear = new NodaTime.LocalDate(today.Year, e.Month!.Value, e.Day!.Value);
                var nextOccurrence = thisYear >= today ? thisYear : thisYear.PlusYears(1);
                var localDateTime = e.IsAllDay
                    ? nextOccurrence.AtMidnight()
                    : nextOccurrence.At(e.TimeOfDay.HasValue ? NodaTime.LocalTime.FromTicksSinceMidnight(e.TimeOfDay.Value.Ticks) : NodaTime.LocalTime.Midnight);
                var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
                var instant = zonedDateTime.ToInstant();
                return instant.ToDateTimeUtc();
            })
            .ToList();

        var monthName = CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetMonthName(month);

        if (monthEvents.Count == 0)
        {
            await _botClient.SendMessage(
                chatId,
                $"События в {monthName} нет",
                cancellationToken: cancellationToken);
            return;
        }

        const int itemsPerPage = 10;
        var totalPages = (monthEvents.Count + itemsPerPage - 1) / itemsPerPage;
        var currentPage = Math.Min(page, totalPages - 1);
        var startIndex = currentPage * itemsPerPage;
        var endIndex = Math.Min(startIndex + itemsPerPage, monthEvents.Count);
        var pageEvents = monthEvents.Skip(startIndex).Take(endIndex - startIndex).ToList();

        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var eventEntity in pageEvents)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"✏️ {eventEntity.Title}", $"event_edit:{eventEntity.Id}")
            });
        }

        if (totalPages > 1)
        {
            var navigationRow = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData(
                    "◀️ Назад",
                    $"event_edit_month_page:{month}:{currentPage - 1}"));
            }
            if (currentPage < totalPages - 1)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData(
                    "Вперёд ▶️",
                    $"event_edit_month_page:{month}:{currentPage + 1}"));
            }
            if (navigationRow.Count > 0)
            {
                buttons.Add(navigationRow);
            }
        }

        var text = $"Выберите событие для редактирования ({monthName}, страница {currentPage + 1} из {totalPages}):";

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                chatId,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleEventListMonthAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный номер месяца",
                cancellationToken: cancellationToken);
            return;
        }

        await SendEventListMonthPageAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, month, 0, null, cancellationToken);
    }

    private async Task HandleEventListMonthPageAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 3 || !int.TryParse(parts[1], out var month) || month < 1 || month > 12 ||
            !int.TryParse(parts[2], out var page) || page < 0)
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var messageId = callbackQuery.Message?.MessageId;
        await SendEventListMonthPageAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, month, page, messageId, cancellationToken);
    }

    private async Task SendEventListMonthPageAsync(long chatId, int month, int page, int? messageId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = NodaTime.DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = NodaTime.Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var allUpcomingEvents = await _listUpcomingItems.ExecuteAsync(cancellationToken);
        
        var monthEvents = allUpcomingEvents
            .Where(e =>
            {
                var eventMonth = 0;

                if (e.Kind == Domain.Enums.EventKind.OneOff && e.OccursAtUtc.HasValue)
                {
                    var eventInstant = NodaTime.Instant.FromDateTimeUtc(e.OccursAtUtc.Value.UtcDateTime);
                    var eventInZone = eventInstant.InZone(timeZone);
                    eventMonth = eventInZone.Month;
                }
                else if (e.Kind == Domain.Enums.EventKind.Yearly && e.Month.HasValue && e.Day.HasValue)
                {
                    var thisYear = new NodaTime.LocalDate(today.Year, e.Month.Value, e.Day.Value);
                    var nextOccurrence = thisYear >= today ? thisYear : thisYear.PlusYears(1);
                    eventMonth = nextOccurrence.Month;
                }

                return eventMonth == month;
            })
            .OrderBy(e =>
            {
                if (e.Kind == Domain.Enums.EventKind.OneOff && e.OccursAtUtc.HasValue)
                {
                    return e.OccursAtUtc.Value.UtcDateTime;
                }

                var thisYear = new NodaTime.LocalDate(today.Year, e.Month!.Value, e.Day!.Value);
                var nextOccurrence = thisYear >= today ? thisYear : thisYear.PlusYears(1);
                var localDateTime = e.IsAllDay
                    ? nextOccurrence.AtMidnight()
                    : nextOccurrence.At(e.TimeOfDay.HasValue ? NodaTime.LocalTime.FromTicksSinceMidnight(e.TimeOfDay.Value.Ticks) : NodaTime.LocalTime.Midnight);
                var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
                var instant = zonedDateTime.ToInstant();
                return instant.ToDateTimeUtc();
            })
            .ToList();

        var monthName = CultureInfo.GetCultureInfo("ru-RU").DateTimeFormat.GetMonthName(month);

        if (monthEvents.Count == 0)
        {
            await _botClient.SendMessage(
                chatId,
                $"События в {monthName} отсутствуют",
                cancellationToken: cancellationToken);
            return;
        }

        const int itemsPerPage = 10;
        var totalPages = (monthEvents.Count + itemsPerPage - 1) / itemsPerPage;
        var currentPage = Math.Min(page, totalPages - 1);
        var startIndex = currentPage * itemsPerPage;
        var endIndex = Math.Min(startIndex + itemsPerPage, monthEvents.Count);
        var pageEvents = monthEvents.Skip(startIndex).Take(endIndex - startIndex).ToList();

        var eventAttachments = new Dictionary<int, Attachment?>();
        foreach (var eventEntity in pageEvents)
        {
            var currentAttachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventEntity.Id, cancellationToken);
            eventAttachments[eventEntity.Id] = currentAttachment;
        }

        var text = _eventFormatter.Format(pageEvents, eventAttachments);
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var eventEntity in pageEvents)
        {
            var currentAttachment = eventAttachments.ContainsKey(eventEntity.Id) ? eventAttachments[eventEntity.Id] : null;
            var attachmentIndicator = currentAttachment != null ? " 📎" : "";
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"📅 {eventEntity.Title}{attachmentIndicator}", $"event_view:{eventEntity.Id}")
            });
        }

        if (totalPages > 1)
        {
            var navigationRow = new List<InlineKeyboardButton>();
            if (currentPage > 0)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData(
                    "◀️ Назад",
                    $"event_list_month_page:{month}:{currentPage - 1}"));
            }
            if (currentPage < totalPages - 1)
            {
                navigationRow.Add(InlineKeyboardButton.WithCallbackData(
                    "Вперёд ▶️",
                    $"event_list_month_page:{month}:{currentPage + 1}"));
            }
            if (navigationRow.Count > 0)
            {
                buttons.Add(navigationRow);
            }
        }

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔙 Назад к месяцам", "cmd:event_list")
        });

        var pageInfo = totalPages > 1 ? $" (страница {currentPage + 1} из {totalPages})" : "";
        var fullText = $"{monthName}{pageInfo}:\n\n{text}";

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                fullText,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                chatId,
                fullText,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleEventViewWeekAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var weekOffset))
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var messageId = callbackQuery.Message?.MessageId;
        await SendEventViewWeekAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, weekOffset, messageId, cancellationToken);
    }

    private async Task HandleEventViewMonthAsync(CallbackQuery callbackQuery, string data, CancellationToken cancellationToken)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQuery.Id,
            cancellationToken: cancellationToken);

        var parts = data.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var monthOffset))
        {
            await _botClient.SendMessage(
                callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id,
                "Ошибка: неверный формат данных",
                cancellationToken: cancellationToken);
            return;
        }

        var messageId = callbackQuery.Message?.MessageId;
        await SendEventViewMonthAsync(callbackQuery.Message?.Chat.Id ?? callbackQuery.From!.Id, monthOffset, messageId, cancellationToken);
    }

    internal async Task SendEventViewWeekAsync(long chatId, int weekOffset, int? messageId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = NodaTime.DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = NodaTime.Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var targetDate = today.PlusWeeks(weekOffset);
        var daysFromMonday = ((int)targetDate.DayOfWeek - (int)NodaTime.IsoDayOfWeek.Monday + 7) % 7;
        var weekStart = targetDate.PlusDays(-daysFromMonday);
        var weekEnd = weekStart.PlusDays(6);

        var periodStart = weekStart.AtMidnight();
        var periodEnd = weekEnd.At(NodaTime.LocalTime.MaxValue);

        var periodStartInstant = periodStart.InZoneLeniently(timeZone).ToInstant();
        var periodEndInstant = periodEnd.InZoneLeniently(timeZone).ToInstant();

        var allEvents = await _listUpcomingItems.ExecuteAsync(cancellationToken);
        var weekEvents = new List<Domain.Entities.Event>();

        foreach (var eventEntity in allEvents)
        {
            NodaTime.Instant? eventInstant = null;

            if (eventEntity.Kind == Domain.Enums.EventKind.OneOff && eventEntity.OccursAtUtc.HasValue)
            {
                eventInstant = NodaTime.Instant.FromDateTimeUtc(eventEntity.OccursAtUtc.Value.UtcDateTime);
            }
            else if (eventEntity.Kind == Domain.Enums.EventKind.Yearly && eventEntity.Month.HasValue && eventEntity.Day.HasValue)
            {
                var yearlyDate = new NodaTime.LocalDate(weekStart.Year, eventEntity.Month.Value, eventEntity.Day.Value);
                if (yearlyDate < weekStart)
                {
                    yearlyDate = new NodaTime.LocalDate(weekStart.Year + 1, eventEntity.Month.Value, eventEntity.Day.Value);
                }

                if (yearlyDate >= weekStart && yearlyDate <= weekEnd)
                {
                    var localDateTime = eventEntity.IsAllDay
                        ? yearlyDate.AtMidnight()
                        : yearlyDate.At(eventEntity.TimeOfDay.HasValue ? NodaTime.LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks) : NodaTime.LocalTime.Midnight);
                    eventInstant = localDateTime.InZoneLeniently(timeZone).ToInstant();
                }
            }

            if (eventInstant.HasValue && eventInstant.Value >= periodStartInstant && eventInstant.Value <= periodEndInstant)
            {
                weekEvents.Add(eventEntity);
            }
        }

        weekEvents = weekEvents.OrderBy(e =>
        {
            if (e.Kind == Domain.Enums.EventKind.OneOff && e.OccursAtUtc.HasValue)
            {
                return e.OccursAtUtc.Value.UtcDateTime;
            }

            var yearlyDate = new NodaTime.LocalDate(weekStart.Year, e.Month!.Value, e.Day!.Value);
            if (yearlyDate < weekStart)
            {
                yearlyDate = new NodaTime.LocalDate(weekStart.Year + 1, e.Month.Value, e.Day.Value);
            }
            var localDateTime = e.IsAllDay
                ? yearlyDate.AtMidnight()
                : yearlyDate.At(e.TimeOfDay.HasValue ? NodaTime.LocalTime.FromTicksSinceMidnight(e.TimeOfDay.Value.Ticks) : NodaTime.LocalTime.Midnight);
            var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
            var instant = zonedDateTime.ToInstant();
            return instant.ToDateTimeUtc();
        }).ToList();

        var allBirthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        var weekBirthdays = allBirthdays
            .Where(b =>
            {
                var birthdayDate = new NodaTime.LocalDate(weekStart.Year, b.Month, b.Day);
                if (birthdayDate < weekStart)
                {
                    birthdayDate = new NodaTime.LocalDate(weekStart.Year + 1, b.Month, b.Day);
                }
                return birthdayDate >= weekStart && birthdayDate <= weekEnd;
            })
            .OrderBy(b =>
            {
                var birthdayDate = new NodaTime.LocalDate(weekStart.Year, b.Month, b.Day);
                if (birthdayDate < weekStart)
                {
                    birthdayDate = new NodaTime.LocalDate(weekStart.Year + 1, b.Month, b.Day);
                }
                return birthdayDate;
            })
            .ToList();

        var eventAttachments = new Dictionary<int, Attachment?>();
        foreach (var eventEntity in weekEvents)
        {
            var currentAttachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventEntity.Id, cancellationToken);
            eventAttachments[eventEntity.Id] = currentAttachment;
        }

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var weekText = $"{weekStart.ToString("d MMMM", culture)} — {weekEnd.ToString("d MMMM yyyy", culture)}";
        
        var textBuilder = new StringBuilder();
        textBuilder.AppendLine($"Календарь на неделю ({weekText}):");
        textBuilder.AppendLine();

        if (weekEvents.Count == 0 && weekBirthdays.Count == 0)
        {
            textBuilder.AppendLine("События и дни рождения отсутствуют");
        }
        else
        {
            if (weekEvents.Count > 0)
            {
                textBuilder.AppendLine("📅 События:");
                textBuilder.AppendLine(_eventFormatter.Format(weekEvents, eventAttachments));
                if (weekBirthdays.Count > 0)
                {
                    textBuilder.AppendLine();
                }
            }

            if (weekBirthdays.Count > 0)
            {
                textBuilder.AppendLine("🎂 Дни рождения:");
                textBuilder.AppendLine(_birthdayFormatter.Format(weekBirthdays));
            }
        }

        var text = textBuilder.ToString();

        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var eventEntity in weekEvents)
        {
            var currentAttachment = eventAttachments.ContainsKey(eventEntity.Id) ? eventAttachments[eventEntity.Id] : null;
            var attachmentIndicator = currentAttachment != null ? " 📎" : "";
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"📅 {eventEntity.Title}{attachmentIndicator}", $"event_view:{eventEntity.Id}")
            });
        }

        foreach (var birthday in weekBirthdays)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"🎂 {birthday.PersonName}", $"birthday_edit:{birthday.Id}")
            });
        }

        var navigationRow = new List<InlineKeyboardButton>();
        navigationRow.Add(InlineKeyboardButton.WithCallbackData("◀️ Предыдущая неделя", $"event_view_week:{weekOffset - 1}"));
        navigationRow.Add(InlineKeyboardButton.WithCallbackData("Следующая неделя ▶️", $"event_view_week:{weekOffset + 1}"));
        buttons.Add(navigationRow);

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔙 Меню событий", "menu:events")
        });

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                chatId,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
    }

    internal async Task SendEventViewMonthAsync(long chatId, int monthOffset, int? messageId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var timeZone = NodaTime.DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var nowInZone = NodaTime.Instant.FromDateTimeUtc(now).InZone(timeZone);
        var today = nowInZone.Date;

        var targetMonth = today.PlusMonths(monthOffset);
        var periodStart = new NodaTime.LocalDate(targetMonth.Year, targetMonth.Month, 1).AtMidnight();
        var lastDayOfMonth = NodaTime.CalendarSystem.Iso.GetDaysInMonth(targetMonth.Year, targetMonth.Month);
        var periodEnd = new NodaTime.LocalDate(targetMonth.Year, targetMonth.Month, lastDayOfMonth).At(NodaTime.LocalTime.MaxValue);

        var periodStartInstant = periodStart.InZoneLeniently(timeZone).ToInstant();
        var periodEndInstant = periodEnd.InZoneLeniently(timeZone).ToInstant();

        var allEvents = await _listUpcomingItems.ExecuteAsync(cancellationToken);
        var monthEvents = new List<Domain.Entities.Event>();

        foreach (var eventEntity in allEvents)
        {
            NodaTime.Instant? eventInstant = null;

            if (eventEntity.Kind == Domain.Enums.EventKind.OneOff && eventEntity.OccursAtUtc.HasValue)
            {
                eventInstant = NodaTime.Instant.FromDateTimeUtc(eventEntity.OccursAtUtc.Value.UtcDateTime);
            }
            else if (eventEntity.Kind == Domain.Enums.EventKind.Yearly && eventEntity.Month.HasValue && eventEntity.Day.HasValue)
            {
                var targetDate = new NodaTime.LocalDate(targetMonth.Year, eventEntity.Month.Value, eventEntity.Day.Value);
                if (targetDate >= periodStart.Date && targetDate <= periodEnd.Date)
                {
                    var localDateTime = eventEntity.IsAllDay
                        ? targetDate.AtMidnight()
                        : targetDate.At(eventEntity.TimeOfDay.HasValue ? NodaTime.LocalTime.FromTicksSinceMidnight(eventEntity.TimeOfDay.Value.Ticks) : NodaTime.LocalTime.Midnight);
                    eventInstant = localDateTime.InZoneLeniently(timeZone).ToInstant();
                }
            }

            if (eventInstant.HasValue && eventInstant.Value >= periodStartInstant && eventInstant.Value <= periodEndInstant)
            {
                monthEvents.Add(eventEntity);
            }
        }

        monthEvents = monthEvents.OrderBy(e =>
        {
            if (e.Kind == Domain.Enums.EventKind.OneOff && e.OccursAtUtc.HasValue)
            {
                return e.OccursAtUtc.Value.UtcDateTime;
            }

            var targetDate = new NodaTime.LocalDate(targetMonth.Year, e.Month!.Value, e.Day!.Value);
            var localDateTime = e.IsAllDay
                ? targetDate.AtMidnight()
                : targetDate.At(e.TimeOfDay.HasValue ? NodaTime.LocalTime.FromTicksSinceMidnight(e.TimeOfDay.Value.Ticks) : NodaTime.LocalTime.Midnight);
            var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
            var instant = zonedDateTime.ToInstant();
            return instant.ToDateTimeUtc();
        }).ToList();

        var allBirthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        var monthBirthdays = allBirthdays
            .Where(b => b.Month == targetMonth.Month)
            .OrderBy(b => b.Day)
            .ToList();

        var eventAttachments = new Dictionary<int, Attachment?>();
        foreach (var eventEntity in monthEvents)
        {
            var currentAttachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventEntity.Id, cancellationToken);
            eventAttachments[eventEntity.Id] = currentAttachment;
        }

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var monthName = targetMonth.ToString("MMMM yyyy", culture);
        
        var textBuilder = new StringBuilder();
        textBuilder.AppendLine($"Календарь на {monthName}:");
        textBuilder.AppendLine();

        if (monthEvents.Count == 0 && monthBirthdays.Count == 0)
        {
            textBuilder.AppendLine("События и дни рождения отсутствуют");
        }
        else
        {
            if (monthEvents.Count > 0)
            {
                textBuilder.AppendLine("📅 События:");
                textBuilder.AppendLine(_eventFormatter.Format(monthEvents, eventAttachments));
                if (monthBirthdays.Count > 0)
                {
                    textBuilder.AppendLine();
                }
            }

            if (monthBirthdays.Count > 0)
            {
                textBuilder.AppendLine("🎂 Дни рождения:");
                textBuilder.AppendLine(_birthdayFormatter.Format(monthBirthdays));
            }
        }

        var text = textBuilder.ToString();

        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var eventEntity in monthEvents)
        {
            var currentAttachment = eventAttachments.ContainsKey(eventEntity.Id) ? eventAttachments[eventEntity.Id] : null;
            var attachmentIndicator = currentAttachment != null ? " 📎" : "";
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"📅 {eventEntity.Title}{attachmentIndicator}", $"event_view:{eventEntity.Id}")
            });
        }

        foreach (var birthday in monthBirthdays)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"🎂 {birthday.PersonName}", $"birthday_edit:{birthday.Id}")
            });
        }

        var navigationRow = new List<InlineKeyboardButton>();
        navigationRow.Add(InlineKeyboardButton.WithCallbackData("◀️ Предыдущий месяц", $"event_view_month:{monthOffset - 1}"));
        navigationRow.Add(InlineKeyboardButton.WithCallbackData("Следующий месяц ▶️", $"event_view_month:{monthOffset + 1}"));
        buttons.Add(navigationRow);

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("🔙 Меню событий", "menu:events")
        });

        if (messageId.HasValue)
        {
            await _botClient.EditMessageText(
                chatId,
                messageId.Value,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
        {
            await _botClient.SendMessage(
                chatId,
                text,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
    }
}
