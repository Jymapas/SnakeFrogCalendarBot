using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
using SnakeFrogCalendarBot.Worker.Api;
using SnakeFrogCalendarBot.Worker.Config;
using SnakeFrogCalendarBot.Worker.Telegram.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using IClock = SnakeFrogCalendarBot.Application.Abstractions.Time.IClock;

namespace SnakeFrogCalendarBot.Worker.Telegram.Handlers;

public sealed class MessageHandlers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ITelegramBotClient _botClient;
    private readonly IConversationStateRepository _conversationRepository;
    private readonly IBirthdayDateParser _birthdayDateParser;
    private readonly IDateTimeParser _dateTimeParser;
    private readonly CreateBirthday _createBirthday;
    private readonly CreateEvent _createEvent;
    private readonly UpdateEvent _updateEvent;
    private readonly UpdateBirthday _updateBirthday;
    private readonly AttachFileToEvent _attachFileToEvent;
    private readonly ReplaceEventFile _replaceEventFile;
    private readonly IEventRepository _eventRepository;
    private readonly IBirthdayRepository _birthdayRepository;
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _miniAppUrl;
    private readonly MiniAppTokenService _tokenService;

    public MessageHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IBirthdayDateParser birthdayDateParser,
        IDateTimeParser dateTimeParser,
        CreateBirthday createBirthday,
        CreateEvent createEvent,
        UpdateEvent updateEvent,
        UpdateBirthday updateBirthday,
        AttachFileToEvent attachFileToEvent,
        ReplaceEventFile replaceEventFile,
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository,
        IClock clock,
        ITimeZoneProvider timeZoneProvider,
        IServiceProvider serviceProvider,
        AppOptions appOptions,
        MiniAppTokenService tokenService)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _birthdayDateParser = birthdayDateParser;
        _dateTimeParser = dateTimeParser;
        _createBirthday = createBirthday;
        _createEvent = createEvent;
        _updateEvent = updateEvent;
        _updateBirthday = updateBirthday;
        _attachFileToEvent = attachFileToEvent;
        _replaceEventFile = replaceEventFile;
        _eventRepository = eventRepository;
        _birthdayRepository = birthdayRepository;
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
        _serviceProvider = serviceProvider;
        _miniAppUrl = appOptions.MiniAppUrl;
        _tokenService = tokenService;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From?.Id is not { } userId)
        {
            return;
        }

        var state = await _conversationRepository.GetByUserIdAsync(userId, cancellationToken);
        
        if (state is not null && string.Equals(state.ConversationName, ConversationNames.WaitingForEventFile, StringComparison.OrdinalIgnoreCase))
        {
            await HandleEventFileAsync(message, state, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Text))
        {
            if (state is not null)
            {
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Пожалуйста, отправьте текстовое сообщение",
                    cancellationToken: cancellationToken);
            }
            return;
        }

        if (state is null)
        {
            // Обработка нажатий на кнопки клавиатуры
            var text = message.Text.Trim();
            if (await HandleKeyboardButtonAsync(message, text, cancellationToken))
            {
                return;
            }

            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Используйте /birthday_add, /birthday_list, /event_add или /event_list",
                cancellationToken: cancellationToken);
            return;
        }

        if (string.Equals(state.ConversationName, ConversationNames.BirthdayAdd, StringComparison.OrdinalIgnoreCase))
        {
            await HandleBirthdayAddAsync(message, state, cancellationToken);
        }
        else if (string.Equals(state.ConversationName, ConversationNames.BirthdayEdit, StringComparison.OrdinalIgnoreCase))
        {
            await HandleBirthdayEditAsync(message, state, cancellationToken);
        }
        else if (string.Equals(state.ConversationName, ConversationNames.EventAdd, StringComparison.OrdinalIgnoreCase))
        {
            await HandleEventAddAsync(message, state, cancellationToken);
        }
        else if (string.Equals(state.ConversationName, ConversationNames.EventEdit, StringComparison.OrdinalIgnoreCase))
        {
            await HandleEventEditAsync(message, state, cancellationToken);
        }
        else
        {
            await _conversationRepository.DeleteAsync(userId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Состояние диалога сброшено. Начните заново",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleEventFileAsync(Message message, ConversationState state, CancellationToken cancellationToken)
    {
        var isReplace = state.Step.StartsWith("replace:");
        var eventIdStr = isReplace ? state.Step.Split(':')[1] : state.Step;
        
        if (!int.TryParse(eventIdStr, out var eventId))
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        if (!isReplace && !string.IsNullOrWhiteSpace(message.Text))
        {
            var text = message.Text.Trim();
            if (string.Equals(text, "готово", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "done", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "✅ Готово", StringComparison.OrdinalIgnoreCase))
            {
                await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Готово. Все файлы прикреплены.",
                    cancellationToken: cancellationToken);
                return;
            }
        }

        string? fileId = null;
        string? fileUniqueId = null;
        string? fileName = null;
        string? mimeType = null;
        long? size = null;

        if (message.Document is not null)
        {
            fileId = message.Document.FileId;
            fileUniqueId = message.Document.FileUniqueId;
            fileName = message.Document.FileName;
            mimeType = message.Document.MimeType;
            size = message.Document.FileSize;
        }
        else if (message.Photo is not null && message.Photo.Length > 0)
        {
            var photo = message.Photo[^1];
            fileId = photo.FileId;
            fileUniqueId = photo.FileUniqueId;
            fileName = "photo.jpg";
            mimeType = "image/jpeg";
            size = photo.FileSize;
        }
        else if (message.Video is not null)
        {
            fileId = message.Video.FileId;
            fileUniqueId = message.Video.FileUniqueId;
            fileName = message.Video.FileName ?? "video.mp4";
            mimeType = message.Video.MimeType;
            size = message.Video.FileSize;
        }
        else if (message.Audio is not null)
        {
            fileId = message.Audio.FileId;
            fileUniqueId = message.Audio.FileUniqueId;
            fileName = message.Audio.FileName ?? "audio.mp3";
            mimeType = message.Audio.MimeType;
            size = message.Audio.FileSize;
        }
        else
        {
            await _botClient.SendNoPreview(
                message.Chat.Id,
                isReplace
                    ? "Пожалуйста, отправьте файл"
                    : "Пожалуйста, отправьте файл или нажмите «Готово»",
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
            return;
        }

        try
        {
            if (isReplace)
            {
                var replaceCommand = new ReplaceEventFileCommand(
                    eventId,
                    fileId,
                    fileUniqueId,
                    fileName ?? "file",
                    mimeType,
                    size);

                await _replaceEventFile.ExecuteAsync(replaceCommand, cancellationToken);
                await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);

                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Файл успешно заменён",
                    cancellationToken: cancellationToken);
            }
            else
            {
                var attachCommand = new AttachFileToEventCommand(
                    eventId,
                    fileId,
                    fileUniqueId,
                    fileName ?? "file",
                    mimeType,
                    size);

                await _attachFileToEvent.ExecuteAsync(attachCommand, cancellationToken);
                state.Update(state.Step, state.StateJson, _clock.UtcNow);
                await _conversationRepository.UpsertAsync(state, cancellationToken);

                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Файл успешно прикреплён. Отправьте ещё или нажмите «Готово».",
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("✅ Готово", $"event_attach_done:{eventId}"),
                            InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                        }
                    }),
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _botClient.SendNoPreview(
                message.Chat.Id,
                $"Ошибка: {ex.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task HandleBirthdayAddAsync(Message message, ConversationState state, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var data = DeserializeData(state.StateJson);
        var now = _clock.UtcNow;

        switch (state.Step)
        {
            case BirthdayConversationSteps.Name:
                var isMultiline = text.Contains('\n');
                if (isMultiline)
                {
                    if (TryParseMultilineBirthday(text, out var parsedName, out var parsedDay, out var parsedMonth, out var parsedYear, out var parsedContact))
                    {
                        var command = new CreateBirthdayCommand(
                            parsedName,
                            parsedDay,
                            parsedMonth,
                            parsedYear,
                            parsedContact);

                        await _createBirthday.ExecuteAsync(command, cancellationToken);
                        await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);

                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Сохранено",
                            cancellationToken: cancellationToken);
                        return;
                    }
                    else
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать формат. Используйте:\nИмя\nдд MMMM [YYYY]\n[контакт]\n\nИли введите имя для пошагового ввода",
                            cancellationToken: cancellationToken);
                        return;
                    }
                }

                data.PersonName = text;
                await UpdateStateAsync(state, BirthdayConversationSteps.Date, data, now, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Введите дату (например, 7 января)",
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.Date:
                if (!_birthdayDateParser.TryParseMonthDay(text, out var day, out var month))
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Не удалось распознать дату. Формат: 7 января или 10 июня 1973",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Day = day;
                data.Month = month;

                // Проверяем, есть ли год в строке
                if (TryParseYearFromDateLine(text, out var year))
                {
                    data.BirthYear = year;
                    // Если год найден, пропускаем шаг BirthYear и переходим к Contact
                    await UpdateStateAsync(state, BirthdayConversationSteps.Contact, data, now, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Введите контакт или 'пропустить'",
                        replyMarkup: CreateSkipKeyboard(ConversationNames.BirthdayAdd, BirthdayConversationSteps.Contact),
                        cancellationToken: cancellationToken);
                }
                else
                {
                    // Год не найден, переходим к шагу ввода года
                    await UpdateStateAsync(state, BirthdayConversationSteps.BirthYear, data, now, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Введите год рождения или 'пропустить'",
                        replyMarkup: CreateSkipKeyboard(ConversationNames.BirthdayAdd, BirthdayConversationSteps.BirthYear),
                        cancellationToken: cancellationToken);
                }
                break;

            case BirthdayConversationSteps.BirthYear:
                if (IsSkip(text))
                {
                    data.BirthYear = null;
                }
                else if (!int.TryParse(text, out var birthYear) || birthYear <= 0)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Введите год рождения или 'пропустить'",
                        replyMarkup: CreateSkipKeyboard(ConversationNames.BirthdayAdd, BirthdayConversationSteps.BirthYear),
                        cancellationToken: cancellationToken);
                    return;
                }
                else
                {
                    data.BirthYear = birthYear;
                }

                await UpdateStateAsync(state, BirthdayConversationSteps.Contact, data, now, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Введите контакт или 'пропустить'",
                    replyMarkup: CreateSkipKeyboard(ConversationNames.BirthdayAdd, BirthdayConversationSteps.Contact),
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.Contact:
                data.Contact = IsSkip(text) ? null : text;
                await SaveBirthdayAsync(message, data, cancellationToken);
                break;

            default:
                await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Состояние диалога сброшено. Начните заново с /birthday_add",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task SaveBirthdayAsync(Message message, BirthdayConversationData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(data.PersonName) || data.Day is null || data.Month is null)
        {
            await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Не удалось сохранить запись. Начните заново с /birthday_add",
                cancellationToken: cancellationToken);
            return;
        }

        var command = new CreateBirthdayCommand(
            data.PersonName,
            data.Day.Value,
            data.Month.Value,
            data.BirthYear,
            data.Contact);

        await _createBirthday.ExecuteAsync(command, cancellationToken);
        await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);

        await _botClient.SendNoPreview(
            message.Chat.Id,
            "Сохранено",
            cancellationToken: cancellationToken);
    }

    private async Task UpdateStateAsync(
        ConversationState state,
        string nextStep,
        BirthdayConversationData data,
        DateTime now,
        CancellationToken cancellationToken)
    {
        state.Update(nextStep, SerializeData(data), now);
        await _conversationRepository.UpsertAsync(state, cancellationToken);
    }

    private static BirthdayConversationData DeserializeData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BirthdayConversationData();
        }

        return JsonSerializer.Deserialize<BirthdayConversationData>(json, JsonOptions)
            ?? new BirthdayConversationData();
    }

    private static string SerializeData(BirthdayConversationData data)
    {
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private async Task HandleEventAddAsync(Message message, ConversationState state, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var data = DeserializeEventData(state.StateJson);
        var now = _clock.UtcNow;

        switch (state.Step)
        {
            case EventConversationSteps.Title:
                var isMultiline = text.Contains('\n');
                if (isMultiline)
                {
                    if (TryParseMultilineEvent(text, out var parsedEventData))
                    {
                        await SaveEventFromDataAsync(message, parsedEventData, cancellationToken);
                        return;
                    }
                    else
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать формат. Используйте:\nНазвание\nдата/время [разовое|ежегодное]\n[описание]\n[место]\n[ссылка]\n\nМожно использовать маркеры: место:, ссылка:, описание:\nСсылки определяются автоматически\n\nИли введите название для пошагового ввода",
                            cancellationToken: cancellationToken);
                        return;
                    }
                }

                data.Title = text;
                await UpdateEventStateAsync(state, EventConversationSteps.Kind, data, now, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Выберите тип события",
                    replyMarkup: InlineKeyboards.EventKindKeyboard(),
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Date:
                if (!_dateTimeParser.TryParse(text, out var parseResult) || parseResult is null)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Не удалось распознать дату. Формат: 7 января 2026 или 2026-01-07",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Year = parseResult.Year;
                data.Month = parseResult.Month;
                data.Day = parseResult.Day;
                data.Hour = parseResult.Hour;
                data.Minute = parseResult.Minute;
                data.HasYear = parseResult.HasYear;

                if (parseResult.Hour.HasValue && parseResult.Minute.HasValue)
                {
                    data.IsAllDay = false;
                    await AskDescriptionAsync(state, data, message.Chat.Id, now, cancellationToken);
                }
                else
                {
                    await UpdateEventStateAsync(state, EventConversationSteps.AllDay, data, now, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Это событие на весь день? (да/нет)",
                        cancellationToken: cancellationToken);
                }
                break;

            case EventConversationSteps.AllDay:
                var isAllDay = text.Equals("да", StringComparison.OrdinalIgnoreCase) ||
                               text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                               text.Equals("y", StringComparison.OrdinalIgnoreCase);
                data.IsAllDay = isAllDay;

                if (!isAllDay)
                {
                    await UpdateEventStateAsync(state, EventConversationSteps.Time, data, now, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Введите время (HH:mm)",
                        cancellationToken: cancellationToken);
                }
                else if (IsRecurringKind(data.Kind))
                {
                    await AskRepeatUntilAsync(state, data, message.Chat.Id, now, cancellationToken);
                }
                else
                {
                    await AskDescriptionAsync(state, data, message.Chat.Id, now, cancellationToken);
                }
                break;

            case EventConversationSteps.Time:
                if (!_dateTimeParser.TryParse(text, out var timeResult) || timeResult is null ||
                    !timeResult.Hour.HasValue || !timeResult.Minute.HasValue)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Не удалось распознать время. Формат: HH:mm",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Hour = timeResult.Hour;
                data.Minute = timeResult.Minute;

                if (IsRecurringKind(data.Kind))
                {
                    await AskRepeatUntilAsync(state, data, message.Chat.Id, now, cancellationToken);
                }
                else
                {
                    await AskDescriptionAsync(state, data, message.Chat.Id, now, cancellationToken);
                }
                break;

            case EventConversationSteps.Kind:
                var kind = ParseEventKind(text);
                if (kind is null)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Выберите тип события кнопкой или введите: разовое, ежегодное, еженедельно, периодически",
                        replyMarkup: InlineKeyboards.EventKindKeyboard(),
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Kind = kind;

                switch (kind)
                {
                    case "Weekly":
                        data.DaysOfWeekMask = 0;
                        await UpdateEventStateAsync(state, EventConversationSteps.Weekdays, data, now, cancellationToken);
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Выберите дни недели и нажмите «Готово»",
                            replyMarkup: InlineKeyboards.WeekdaysKeyboard(0),
                            cancellationToken: cancellationToken);
                        break;

                    case "Interval":
                        await UpdateEventStateAsync(state, EventConversationSteps.IntervalCount, data, now, cancellationToken);
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Как часто повторять? Введите число, например 2 (каждые два периода)",
                            cancellationToken: cancellationToken);
                        break;

                    default:
                        await UpdateEventStateAsync(state, EventConversationSteps.Date, data, now, cancellationToken);
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Введите дату (например, 7 января 2026 или 2026-01-07)",
                            cancellationToken: cancellationToken);
                        break;
                }

                break;

            case EventConversationSteps.Weekdays:
                var toggled = ParseWeekdayMask(text);
                if (toggled is null or 0)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Выберите хотя бы один день недели кнопками",
                        replyMarkup: InlineKeyboards.WeekdaysKeyboard(data.DaysOfWeekMask ?? 0),
                        cancellationToken: cancellationToken);
                    return;
                }

                data.DaysOfWeekMask = toggled;
                await AskRecurrenceStartDateAsync(state, data, message.Chat.Id, now, cancellationToken);
                break;

            case EventConversationSteps.IntervalCount:
                if (!int.TryParse(text, out var intervalCount) || intervalCount < 1)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Введите целое число не меньше 1",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.IntervalCount = intervalCount;
                await UpdateEventStateAsync(state, EventConversationSteps.IntervalUnit, data, now, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    $"Каждые {intervalCount} — чего именно?",
                    replyMarkup: InlineKeyboards.IntervalUnitKeyboard(),
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.IntervalUnit:
                var unit = ParseRecurrenceUnit(text);
                if (unit is null)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Выберите единицу кнопкой: дни, недели, месяцы или годы",
                        replyMarkup: InlineKeyboards.IntervalUnitKeyboard(),
                        cancellationToken: cancellationToken);
                    return;
                }

                data.IntervalUnit = unit;
                await AskRecurrenceStartDateAsync(state, data, message.Chat.Id, now, cancellationToken);
                break;

            case EventConversationSteps.StartDate:
                if (!_dateTimeParser.TryParse(text, out var startResult) || startResult is null || startResult.Year == 0)
                {
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Не удалось распознать дату. Формат: 7 января 2026 или 2026-01-07",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.StartDate = new DateOnly(startResult.Year, startResult.Month, startResult.Day);

                if (startResult.Hour.HasValue && startResult.Minute.HasValue)
                {
                    data.IsAllDay = false;
                    data.Hour = startResult.Hour;
                    data.Minute = startResult.Minute;
                    await AskRepeatUntilAsync(state, data, message.Chat.Id, now, cancellationToken);
                }
                else
                {
                    await UpdateEventStateAsync(state, EventConversationSteps.AllDay, data, now, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Это событие на весь день? (да/нет)",
                        cancellationToken: cancellationToken);
                }

                break;

            case EventConversationSteps.RepeatUntil:
                if (IsSkip(text))
                {
                    data.RepeatUntil = null;
                }
                else
                {
                    if (!_dateTimeParser.TryParse(text, out var untilResult) || untilResult is null || untilResult.Year == 0)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать дату окончания. Формат: 31 декабря 2026 или 2026-12-31",
                            replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.RepeatUntil),
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var repeatUntil = new DateOnly(untilResult.Year, untilResult.Month, untilResult.Day);
                    if (data.StartDate.HasValue && repeatUntil < data.StartDate.Value)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Дата окончания не может быть раньше даты начала",
                            replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.RepeatUntil),
                            cancellationToken: cancellationToken);
                        return;
                    }

                    data.RepeatUntil = repeatUntil;
                }

                await AskDescriptionAsync(state, data, message.Chat.Id, now, cancellationToken);
                break;

            case EventConversationSteps.Description:
                data.Description = IsSkip(text) ? null : text;
                await UpdateEventStateAsync(state, EventConversationSteps.Place, data, now, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Введите место или 'пропустить'",
                    replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.Place),
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Place:
                data.Place = IsSkip(text) ? null : text;
                await UpdateEventStateAsync(state, EventConversationSteps.Link, data, now, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Введите ссылку или 'пропустить'",
                    replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.Link),
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Link:
                data.Link = IsSkip(text) ? null : text;
                await SaveEventAsync(message, data, cancellationToken);
                break;

            default:
                await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Состояние диалога сброшено. Начните заново с /event_add",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task AskEditStartDateAsync(
        ConversationState state,
        EventEditConversationData data,
        long chatId,
        int eventId,
        CancellationToken cancellationToken)
    {
        state.Update(
            $"{EventEditConversationSteps.StartDate}:{eventId}",
            SerializeEventEditData(data),
            _clock.UtcNow);
        await _conversationRepository.UpsertAsync(state, cancellationToken);
        await _botClient.SendNoPreview(
            chatId,
            "С какой даты начинать? (например, 7 января 2026 или 2026-01-07)",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Сохраняет новое расписание повторяющегося события. Тип повторения при этом не меняется.
    /// </summary>
    private async Task UpdateEventScheduleAsync(
        Domain.Entities.Event eventEntity,
        EventEditConversationData data,
        long chatId,
        long userId,
        CancellationToken cancellationToken)
    {
        if (data.StartDate is null)
        {
            throw new InvalidOperationException("StartDate is required for recurring schedule.");
        }

        var isAllDay = data.IsAllDay == true;
        TimeSpan? timeOfDay = isAllDay || !data.Hour.HasValue || !data.Minute.HasValue
            ? null
            : new TimeSpan(data.Hour.Value, data.Minute.Value, 0);

        UpdateEventCommand command;

        if (eventEntity.Kind == Domain.Enums.EventKind.Weekly)
        {
            command = new UpdateEventCommand
            {
                EventId = eventEntity.Id,
                Field = "weeklySchedule",
                DaysOfWeekMask = data.DaysOfWeekMask ?? eventEntity.DaysOfWeekMask,
                StartDateLocal = data.StartDate,
                TimeOfDay = timeOfDay,
                IsAllDay = isAllDay
            };
        }
        else
        {
            command = new UpdateEventCommand
            {
                EventId = eventEntity.Id,
                Field = "intervalSchedule",
                IntervalCount = data.IntervalCount ?? eventEntity.IntervalCount,
                IntervalUnit = Enum.TryParse<Domain.Enums.RecurrenceUnit>(data.IntervalUnit, out var unit)
                    ? unit
                    : eventEntity.IntervalUnit,
                StartDateLocal = data.StartDate,
                TimeOfDay = timeOfDay,
                IsAllDay = isAllDay
            };
        }

        await _updateEvent.ExecuteAsync(command, cancellationToken);
        await _conversationRepository.DeleteAsync(userId, cancellationToken);
        await _botClient.SendNoPreview(
            chatId,
            "Расписание обновлено",
            cancellationToken: cancellationToken);
    }

    private static bool IsRecurringKind(string? kind) => kind is "Weekly" or "Interval";

    /// <summary>
    /// Распознаёт тип события и по нажатию кнопки, и по свободному тексту.
    /// </summary>
    private static string? ParseEventKind(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();

        if (normalized is "oneoff" || normalized.Contains("разов") || normalized.Contains("one"))
        {
            return "OneOff";
        }

        if (normalized is "yearly" || normalized.Contains("ежегод") || normalized.Contains("year"))
        {
            return "Yearly";
        }

        if (normalized is "weekly" || normalized.Contains("еженед") || normalized.Contains("week"))
        {
            return "Weekly";
        }

        if (normalized is "interval" || normalized.Contains("период") || normalized.Contains("интервал")
            || normalized.Contains("раз в") || normalized.Contains("кажд"))
        {
            return "Interval";
        }

        return null;
    }

    private static string? ParseRecurrenceUnit(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();

        if (normalized is "days" || normalized.StartsWith("дн") || normalized.StartsWith("ден"))
        {
            return "Days";
        }

        if (normalized is "weeks" || normalized.StartsWith("недел"))
        {
            return "Weeks";
        }

        if (normalized is "months" || normalized.StartsWith("месяц"))
        {
            return "Months";
        }

        if (normalized is "years" || normalized.StartsWith("год") || normalized.StartsWith("лет"))
        {
            return "Years";
        }

        return null;
    }

    /// <summary>
    /// Принимает готовую маску из кнопочного шага («mask:5») либо перечисление дней текстом.
    /// </summary>
    private static int? ParseWeekdayMask(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();

        if (normalized.StartsWith("mask:") && int.TryParse(normalized[5..], out var mask))
        {
            return mask is > 0 and <= 0b1111111 ? mask : null;
        }

        var names = new[]
        {
            new[] { "пн", "понедельник" },
            new[] { "вт", "вторник" },
            new[] { "ср", "среда", "среду" },
            new[] { "чт", "четверг" },
            new[] { "пт", "пятница", "пятницу" },
            new[] { "сб", "суббота", "субботу" },
            new[] { "вс", "воскресенье" }
        };

        var result = 0;
        foreach (var token in normalized.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            for (var bit = 0; bit < names.Length; bit++)
            {
                if (names[bit].Any(name => token.StartsWith(name, StringComparison.Ordinal)))
                {
                    result |= 1 << bit;
                    break;
                }
            }
        }

        return result == 0 ? null : result;
    }

    private async Task AskRecurrenceStartDateAsync(
        ConversationState state,
        EventConversationData data,
        long chatId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await UpdateEventStateAsync(state, EventConversationSteps.StartDate, data, now, cancellationToken);
        await _botClient.SendNoPreview(
            chatId,
            "С какой даты начинать? (например, 7 января 2026 или 2026-01-07)",
            cancellationToken: cancellationToken);
    }

    private async Task AskRepeatUntilAsync(
        ConversationState state,
        EventConversationData data,
        long chatId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await UpdateEventStateAsync(state, EventConversationSteps.RepeatUntil, data, now, cancellationToken);
        await _botClient.SendNoPreview(
            chatId,
            "До какой даты повторять? Или 'пропустить' — повторять бессрочно",
            replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.RepeatUntil),
            cancellationToken: cancellationToken);
    }

    private async Task AskDescriptionAsync(
        ConversationState state,
        EventConversationData data,
        long chatId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await UpdateEventStateAsync(state, EventConversationSteps.Description, data, now, cancellationToken);
        await _botClient.SendNoPreview(
            chatId,
            "Введите описание или 'пропустить'",
            replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.Description),
            cancellationToken: cancellationToken);
    }

    private async Task SaveEventAsync(Message message, EventConversationData data, CancellationToken cancellationToken)
    {
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var failure = EventCommandBuilder.TryBuild(data, timeZone, out var command);

        if (failure != EventCommandBuildFailure.None)
        {
            var errorText = failure == EventCommandBuildFailure.MissingSchedule
                ? "Не удалось сохранить событие: расписание повторения заполнено не полностью"
                : "Не удалось сохранить событие. Начните заново с /event_add";

            await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                errorText,
                cancellationToken: cancellationToken);
            return;
        }

        await _createEvent.ExecuteAsync(command!, cancellationToken);
        await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);

        await _botClient.SendNoPreview(
            message.Chat.Id,
            "Событие сохранено",
            cancellationToken: cancellationToken);
    }

    private async Task UpdateEventStateAsync(
        ConversationState state,
        string nextStep,
        EventConversationData data,
        DateTime now,
        CancellationToken cancellationToken)
    {
        state.Update(nextStep, SerializeEventData(data), now);
        await _conversationRepository.UpsertAsync(state, cancellationToken);
    }

    private static EventConversationData DeserializeEventData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new EventConversationData();
        }

        return JsonSerializer.Deserialize<EventConversationData>(json, JsonOptions)
            ?? new EventConversationData();
    }

    private static string SerializeEventData(EventConversationData data)
    {
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private async Task HandleEventEditAsync(Message message, ConversationState state, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var parts = state.Step.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var eventId))
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
        }

        var field = parts[0];
        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Событие не найдено",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            UpdateEventCommand command;

            switch (field)
            {
                case "title":
                    command = new UpdateEventCommand { EventId = eventId, Field = "title", Title = text };
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Название обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "description":
                    var description = IsSkip(text) ? null : text;
                    command = new UpdateEventCommand { EventId = eventId, Field = "description", Description = description };
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Описание обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "place":
                    var place = IsSkip(text) ? null : text;
                    command = new UpdateEventCommand { EventId = eventId, Field = "place", Place = place };
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Место обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "link":
                    var link = IsSkip(text) ? null : text;
                    command = new UpdateEventCommand { EventId = eventId, Field = "link", Link = link };
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Ссылка обновлена",
                        cancellationToken: cancellationToken);
                    break;

                case "date":
                    if (!_dateTimeParser.TryParse(text, out var parseResult) || parseResult is null)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать дату. Формат: 7 января 2026 или 2026-01-07",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var data = DeserializeEventEditData(state.StateJson);
                    data.EventId = eventId;
                    data.Year = parseResult.Year;
                    data.Month = parseResult.Month;
                    data.Day = parseResult.Day;
                    data.Hour = parseResult.Hour;
                    data.Minute = parseResult.Minute;
                    data.HasYear = parseResult.HasYear;

                    if (parseResult.Hour.HasValue && parseResult.Minute.HasValue)
                    {
                        await UpdateEventDateAsync(eventId, data, message.Chat.Id, state.UserId, cancellationToken);
                    }
                    else
                    {
                        var now = _clock.UtcNow;
                        state.Update($"{EventEditConversationSteps.AllDay}:{eventId}", SerializeEventEditData(data), now);
                        await _conversationRepository.UpsertAsync(state, cancellationToken);
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Это событие на весь день? (да/нет)",
                            cancellationToken: cancellationToken);
                    }
                    break;

                case "all-day":
                    var isAllDay = text.Equals("да", StringComparison.OrdinalIgnoreCase) ||
                                   text.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                                   text.Equals("y", StringComparison.OrdinalIgnoreCase);
                    data = DeserializeEventEditData(state.StateJson);
                    data.IsAllDay = isAllDay;

                    if (!isAllDay)
                    {
                        var now = _clock.UtcNow;
                        state.Update($"{EventEditConversationSteps.Time}:{eventId}", SerializeEventEditData(data), now);
                        await _conversationRepository.UpsertAsync(state, cancellationToken);
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Введите время (HH:mm)",
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await UpdateEventDateAsync(eventId, data, message.Chat.Id, state.UserId, cancellationToken);
                    }
                    break;

                case "time":
                    if (!_dateTimeParser.TryParse(text, out var timeResult) || timeResult is null ||
                        !timeResult.Hour.HasValue || !timeResult.Minute.HasValue)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать время. Формат: HH:mm",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    data = DeserializeEventEditData(state.StateJson);
                    data.Hour = timeResult.Hour;
                    data.Minute = timeResult.Minute;
                    await UpdateEventDateAsync(eventId, data, message.Chat.Id, state.UserId, cancellationToken);
                    break;

                case EventEditConversationSteps.Weekdays:
                    var editMask = ParseWeekdayMask(text);
                    if (editMask is null or 0)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Выберите хотя бы один день недели кнопками",
                            replyMarkup: InlineKeyboards.WeekdaysKeyboard(
                                DeserializeEventEditData(state.StateJson).DaysOfWeekMask ?? 0),
                            cancellationToken: cancellationToken);
                        return;
                    }

                    data = DeserializeEventEditData(state.StateJson);
                    data.EventId = eventId;
                    data.DaysOfWeekMask = editMask;
                    await AskEditStartDateAsync(state, data, message.Chat.Id, eventId, cancellationToken);
                    break;

                case EventEditConversationSteps.IntervalCount:
                    if (!int.TryParse(text, out var editIntervalCount) || editIntervalCount < 1)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Введите целое число не меньше 1",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    data = DeserializeEventEditData(state.StateJson);
                    data.EventId = eventId;
                    data.IntervalCount = editIntervalCount;
                    state.Update(
                        $"{EventEditConversationSteps.IntervalUnit}:{eventId}",
                        SerializeEventEditData(data),
                        _clock.UtcNow);
                    await _conversationRepository.UpsertAsync(state, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        $"Каждые {editIntervalCount} — чего именно?",
                        replyMarkup: InlineKeyboards.IntervalUnitKeyboard(),
                        cancellationToken: cancellationToken);
                    break;

                case EventEditConversationSteps.IntervalUnit:
                    var editUnit = ParseRecurrenceUnit(text);
                    if (editUnit is null)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Выберите единицу кнопкой: дни, недели, месяцы или годы",
                            replyMarkup: InlineKeyboards.IntervalUnitKeyboard(),
                            cancellationToken: cancellationToken);
                        return;
                    }

                    data = DeserializeEventEditData(state.StateJson);
                    data.EventId = eventId;
                    data.IntervalUnit = editUnit;
                    await AskEditStartDateAsync(state, data, message.Chat.Id, eventId, cancellationToken);
                    break;

                case EventEditConversationSteps.StartDate:
                    if (!_dateTimeParser.TryParse(text, out var editStart) || editStart is null || editStart.Year == 0)
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать дату. Формат: 7 января 2026 или 2026-01-07",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    data = DeserializeEventEditData(state.StateJson);
                    data.EventId = eventId;
                    data.StartDate = new DateOnly(editStart.Year, editStart.Month, editStart.Day);

                    if (editStart.Hour.HasValue && editStart.Minute.HasValue)
                    {
                        data.IsAllDay = false;
                        data.Hour = editStart.Hour;
                        data.Minute = editStart.Minute;
                        await UpdateEventScheduleAsync(eventEntity, data, message.Chat.Id, state.UserId, cancellationToken);
                    }
                    else
                    {
                        state.Update(
                            $"{EventEditConversationSteps.ScheduleTime}:{eventId}",
                            SerializeEventEditData(data),
                            _clock.UtcNow);
                        await _conversationRepository.UpsertAsync(state, cancellationToken);
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Введите время (HH:mm) или 'весь день'",
                            cancellationToken: cancellationToken);
                    }
                    break;

                case EventEditConversationSteps.ScheduleTime:
                    data = DeserializeEventEditData(state.StateJson);
                    data.EventId = eventId;

                    if (text.Contains("весь день", StringComparison.OrdinalIgnoreCase) || IsSkip(text))
                    {
                        data.IsAllDay = true;
                        data.Hour = null;
                        data.Minute = null;
                    }
                    else
                    {
                        if (!_dateTimeParser.TryParse(text, out var editTime) || editTime is null
                            || !editTime.Hour.HasValue || !editTime.Minute.HasValue)
                        {
                            await _botClient.SendNoPreview(
                                message.Chat.Id,
                                "Не удалось распознать время. Формат: HH:mm или 'весь день'",
                                cancellationToken: cancellationToken);
                            return;
                        }

                        data.IsAllDay = false;
                        data.Hour = editTime.Hour;
                        data.Minute = editTime.Minute;
                    }

                    await UpdateEventScheduleAsync(eventEntity, data, message.Chat.Id, state.UserId, cancellationToken);
                    break;

                case EventEditConversationSteps.RepeatUntil:
                    DateOnly? repeatUntil = null;
                    if (!IsSkip(text))
                    {
                        if (!_dateTimeParser.TryParse(text, out var editUntil) || editUntil is null || editUntil.Year == 0)
                        {
                            await _botClient.SendNoPreview(
                                message.Chat.Id,
                                "Не удалось распознать дату окончания. Формат: 31 декабря 2026 или 'пропустить'",
                                cancellationToken: cancellationToken);
                            return;
                        }

                        repeatUntil = new DateOnly(editUntil.Year, editUntil.Month, editUntil.Day);
                    }

                    await _updateEvent.ExecuteAsync(
                        new UpdateEventCommand
                        {
                            EventId = eventId,
                            Field = "repeatUntil",
                            RepeatUntilLocal = repeatUntil
                        },
                        cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        repeatUntil is null ? "Повторение снова бессрочное" : "Дата окончания обновлена",
                        cancellationToken: cancellationToken);
                    break;

                default:
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Неизвестное поле для редактирования",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            await _botClient.SendNoPreview(
                message.Chat.Id,
                $"Ошибка при обновлении: {ex.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task UpdateEventDateAsync(int eventId, EventEditConversationData data, long chatId, long userId, CancellationToken cancellationToken)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(eventId, cancellationToken);
        if (eventEntity is null)
        {
            throw new InvalidOperationException("Event not found.");
        }

        if (eventEntity.Kind == EventKind.OneOff)
        {
            if (data.Year is null || data.Month is null || data.Day is null)
            {
                throw new InvalidOperationException("Year, Month and Day are required.");
            }

            var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
            var localDate = new LocalDate(data.Year.Value, data.Month.Value, data.Day.Value);
            LocalDateTime localDateTime;

            if (data.IsAllDay == true)
            {
                localDateTime = localDate.AtMidnight();
            }
            else
            {
                var time = data.Hour.HasValue && data.Minute.HasValue
                    ? new LocalTime(data.Hour.Value, data.Minute.Value)
                    : LocalTime.Midnight;
                localDateTime = localDate.At(time);
            }

            var zonedDateTime = localDateTime.InZoneLeniently(timeZone);
            var instant = zonedDateTime.ToInstant();
            var occursAtUtc = instant.ToDateTimeOffset();

            var command = new UpdateEventCommand
            {
                EventId = eventId,
                Field = "occursAtUtc",
                OccursAtUtc = occursAtUtc,
                IsAllDay = data.IsAllDay
            };
            await _updateEvent.ExecuteAsync(command, cancellationToken);
        }
        else
        {
            if (data.Month is null || data.Day is null)
            {
                throw new InvalidOperationException("Month and Day are required.");
            }

            TimeSpan? timeOfDay = null;
            if (data.IsAllDay != true && data.Hour.HasValue && data.Minute.HasValue)
            {
                timeOfDay = new TimeSpan(data.Hour.Value, data.Minute.Value, 0);
            }

            var command = new UpdateEventCommand
            {
                EventId = eventId,
                Field = "yearlyDate",
                Month = data.Month,
                Day = data.Day,
                TimeOfDay = timeOfDay,
                IsAllDay = data.IsAllDay
            };
            await _updateEvent.ExecuteAsync(command, cancellationToken);
        }

        await _conversationRepository.DeleteAsync(userId, cancellationToken);
        await _botClient.SendNoPreview(
            chatId,
            "Дата обновлена",
            cancellationToken: cancellationToken);
    }

    private async Task HandleBirthdayEditAsync(Message message, ConversationState state, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var parts = state.Step.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var birthdayId))
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Ошибка: неверный идентификатор дня рождения",
                cancellationToken: cancellationToken);
            return;
        }

        var field = parts[0];
        var birthday = await _birthdayRepository.GetByIdAsync(birthdayId, cancellationToken);
        if (birthday is null)
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "День рождения не найден",
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            UpdateBirthdayCommand command;

            switch (field)
            {
                case "personName":
                    command = new UpdateBirthdayCommand(birthdayId, "personName", text, null, null, null, null);
                    await _updateBirthday.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Имя обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "date":
                    if (!_birthdayDateParser.TryParseMonthDay(text, out var day, out var month))
                    {
                        await _botClient.SendNoPreview(
                            message.Chat.Id,
                            "Не удалось распознать дату. Формат: 7 января",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    command = new UpdateBirthdayCommand(birthdayId, "date", null, day, month, null, null);
                    await _updateBirthday.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Дата обновлена",
                        cancellationToken: cancellationToken);
                    break;

                case "birthYear":
                    int? birthYear = null;
                    if (!IsSkip(text) && int.TryParse(text, out var year) && year > 0)
                    {
                        birthYear = year;
                    }

                    command = new UpdateBirthdayCommand(birthdayId, "birthYear", null, null, null, birthYear, null);
                    await _updateBirthday.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Год рождения обновлён",
                        cancellationToken: cancellationToken);
                    break;

                case "contact":
                    var contact = IsSkip(text) ? null : text;
                    command = new UpdateBirthdayCommand(birthdayId, "contact", null, null, null, null, contact);
                    await _updateBirthday.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Контакт обновлён",
                        cancellationToken: cancellationToken);
                    break;

                default:
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendNoPreview(
                        message.Chat.Id,
                        "Неизвестное поле для редактирования",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            await _botClient.SendNoPreview(
                message.Chat.Id,
                $"Ошибка: {ex.Message}",
                cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                "Произошла ошибка при обновлении. Попробуйте позже.",
                cancellationToken: cancellationToken);
        }
    }

    private static EventEditConversationData DeserializeEventEditData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new EventEditConversationData();
        }

        return JsonSerializer.Deserialize<EventEditConversationData>(json, JsonOptions)
            ?? new EventEditConversationData();
    }

    private static string SerializeEventEditData(EventEditConversationData data)
    {
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private bool TryParseMultilineBirthday(
        string text,
        out string personName,
        out int day,
        out int month,
        out int? birthYear,
        out string? contact)
    {
        personName = string.Empty;
        day = 0;
        month = 0;
        birthYear = null;
        contact = null;

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length < 2 || lines.Length > 3)
        {
            return false;
        }

        personName = lines[0];
        if (string.IsNullOrWhiteSpace(personName))
        {
            return false;
        }

        var dateLine = lines[1];
        if (!_birthdayDateParser.TryParseMonthDay(dateLine, out day, out month))
        {
            return false;
        }

        if (TryParseYearFromDateLine(dateLine, out var year))
        {
            birthYear = year;
        }

        if (lines.Length == 3)
        {
            contact = lines[2];
            if (string.IsNullOrWhiteSpace(contact))
            {
                contact = null;
            }
        }

        return true;
    }

    private static bool TryParseYearFromDateLine(string dateLine, out int year)
    {
        year = 0;
        
        if (string.IsNullOrWhiteSpace(dateLine))
        {
            return false;
        }

        var trimmed = dateLine.Trim();
        
        if (trimmed.Contains('.'))
        {
            var dotParts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (dotParts.Length >= 3)
            {
                var lastPart = dotParts[^1].Trim();
                if (int.TryParse(lastPart, out var parsedYear) && parsedYear > 0 && parsedYear <= 9999)
                {
                    year = parsedYear;
                    return true;
                }
            }
        }
        
        var spaceParts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (spaceParts.Length >= 3)
        {
            var lastPart = spaceParts[^1].Trim();
            if (int.TryParse(lastPart, out var parsedYear) && parsedYear > 0 && parsedYear <= 9999)
            {
                year = parsedYear;
                return true;
            }
        }
        
        return false;
    }

    private bool TryParseMultilineEvent(string text, out EventConversationData eventData)
    {
        eventData = new EventConversationData();

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length < 2)
        {
            return false;
        }

        eventData.Title = lines[0];
        if (string.IsNullOrWhiteSpace(eventData.Title))
        {
            return false;
        }

        var dateTimeLine = lines[1];
        var kind = ExtractEventKindFromLine(dateTimeLine, out var dateTimeString);

        if (!_dateTimeParser.TryParse(dateTimeString, out var parseResult) || parseResult is null)
        {
            return false;
        }

        eventData.Year = parseResult.Year;
        eventData.Month = parseResult.Month;
        eventData.Day = parseResult.Day;
        eventData.Hour = parseResult.Hour;
        eventData.Minute = parseResult.Minute;
        eventData.HasYear = parseResult.HasYear;
        eventData.IsAllDay = !parseResult.Hour.HasValue || !parseResult.Minute.HasValue;

        if (kind is null)
        {
            kind = parseResult.HasYear ? "OneOff" : "Yearly";
        }

        eventData.Kind = kind;

        for (int i = 2; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var lowerLine = line.ToLowerInvariant();

            if (lowerLine.StartsWith("место:") || lowerLine.StartsWith("place:"))
            {
                var place = line.Substring(line.IndexOf(':') + 1).Trim();
                if (!string.IsNullOrWhiteSpace(place))
                {
                    eventData.Place = place;
                }
            }
            else if (lowerLine.StartsWith("ссылка:") || lowerLine.StartsWith("link:"))
            {
                var link = line.Substring(line.IndexOf(':') + 1).Trim();
                if (!string.IsNullOrWhiteSpace(link))
                {
                    eventData.Link = link;
                }
            }
            else if (lowerLine.StartsWith("описание:") || lowerLine.StartsWith("description:"))
            {
                var description = line.Substring(line.IndexOf(':') + 1).Trim();
                if (!string.IsNullOrWhiteSpace(description))
                {
                    eventData.Description = description;
                }
            }
            else if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && 
                     (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                eventData.Link = line;
            }
            else
            {
                if (eventData.Description is null)
                {
                    eventData.Description = line;
                }
                else if (eventData.Place is null)
                {
                    eventData.Place = line;
                }
                else if (eventData.Link is null)
                {
                    eventData.Link = line;
                }
            }
        }

        return true;
    }

    private static string? ExtractEventKindFromLine(string line, out string dateTimeString)
    {
        dateTimeString = line;
        var lowerLine = line.ToLowerInvariant();

        var kindKeywords = new[]
        {
            ("разов", "OneOff"),
            ("разовое", "OneOff"),
            ("one", "OneOff"),
            ("oneoff", "OneOff"),
            ("ежегод", "Yearly"),
            ("ежегодное", "Yearly"),
            ("yearly", "Yearly")
        };

        foreach (var (keyword, kind) in kindKeywords)
        {
            var index = lowerLine.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                dateTimeString = line.Remove(index).Trim();
                if (string.IsNullOrWhiteSpace(dateTimeString))
                {
                    continue;
                }
                return kind;
            }
        }

        return null;
    }

    private async Task SaveEventFromDataAsync(Message message, EventConversationData data, CancellationToken cancellationToken)
    {
        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var failure = EventCommandBuilder.TryBuild(data, timeZone, out var command);

        if (failure != EventCommandBuildFailure.None)
        {
            var errorText = failure switch
            {
                EventCommandBuildFailure.MissingYear => "Для разового события требуется указать год",
                EventCommandBuildFailure.MissingSchedule => "Не удалось сохранить событие: расписание повторения заполнено не полностью",
                _ => "Не удалось сохранить событие. Проверьте формат"
            };

            await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
            await _botClient.SendNoPreview(
                message.Chat.Id,
                errorText,
                cancellationToken: cancellationToken);
            return;
        }

        await _createEvent.ExecuteAsync(command!, cancellationToken);
        await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);

        await _botClient.SendNoPreview(
            message.Chat.Id,
            "Событие сохранено",
            cancellationToken: cancellationToken);
    }

    private static InlineKeyboardMarkup CreateSkipKeyboard(string conversationName, string step)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ Пропустить", $"skip:{conversationName}:{step}")
            }
        });
    }

    private async Task<bool> HandleKeyboardButtonAsync(Message message, string text, CancellationToken cancellationToken)
    {
        string? command = null;
        
        switch (text)
        {
            case "➕ Событие":
                command = BotCommands.EventAdd;
                break;

            case "➕ День рождения":
                command = BotCommands.BirthdayAdd;
                break;

            case "📅 События":
                command = BotCommands.EventList;
                break;

            case "🎂 Дни рождения":
                // Показываем выбор месяца вместо прямого списка
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Выберите месяц:",
                    replyMarkup: InlineKeyboards.MonthSelectionKeyboard(),
                    cancellationToken: cancellationToken);
                return true;

            case "📅 На неделю":
                using (var scope = _serviceProvider.CreateScope())
                {
                    var callbackHandlers = scope.ServiceProvider.GetRequiredService<CallbackHandlers>();
                    await callbackHandlers.SendEventViewWeekAsync(message.Chat.Id, 0, null, cancellationToken);
                }
                return true;

            case "📅 На месяц":
                using (var scope = _serviceProvider.CreateScope())
                {
                    var callbackHandlers = scope.ServiceProvider.GetRequiredService<CallbackHandlers>();
                    await callbackHandlers.SendEventViewMonthAsync(message.Chat.Id, 0, null, cancellationToken);
                }
                return true;

            case "✏️ Редактировать":
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Выберите, что редактировать:\n" + BotCommands.EventEdit + " - событие\n" + BotCommands.BirthdayEdit + " - день рождения",
                    cancellationToken: cancellationToken);
                return true;

            case "🗑 Удалить":
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Выберите, что удалить:\n" + BotCommands.EventDelete + " - событие\n" + BotCommands.BirthdayDelete + " - день рождения",
                    cancellationToken: cancellationToken);
                return true;

            case "↩️ Отмена":
                command = BotCommands.Cancel;
                break;

            case "❌ Скрыть клавиатуру":
                await _botClient.SendNoPreview(
                    message.Chat.Id,
                    "Клавиатура скрыта. Используйте /start для её восстановления.",
                    replyMarkup: ReplyKeyboards.RemoveKeyboard(),
                    cancellationToken: cancellationToken);
                return true;

            default:
                return false;
        }

        if (command is not null)
        {
            // Создаем виртуальное сообщение с командой и передаем в CommandHandlers
            var virtualMessage = new Message
            {
                From = message.From,
                Date = DateTime.UtcNow,
                Chat = message.Chat,
                Text = command
            };

            using var scope = _serviceProvider.CreateScope();
            var commandHandlers = scope.ServiceProvider.GetRequiredService<CommandHandlers>();
            await commandHandlers.HandleAsync(virtualMessage, cancellationToken);
            return true;
        }

        return false;
    }

    private static bool IsSkip(string text)
    {
        return text.Equals("skip", StringComparison.OrdinalIgnoreCase)
               || text.Equals("пропустить", StringComparison.OrdinalIgnoreCase)
               || text.Equals("-", StringComparison.OrdinalIgnoreCase);
    }
}
