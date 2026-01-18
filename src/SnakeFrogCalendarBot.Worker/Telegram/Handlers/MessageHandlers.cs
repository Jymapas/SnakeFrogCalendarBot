using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Domain.Enums;
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
        ITimeZoneProvider timeZoneProvider)
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
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Пожалуйста, отправьте текстовое сообщение",
                    cancellationToken: cancellationToken);
            }
            return;
        }

        if (state is null)
        {
            await _botClient.SendMessage(
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
            await _botClient.SendMessage(
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
            await _botClient.SendMessage(
                message.Chat.Id,
                "Ошибка: неверный идентификатор события",
                cancellationToken: cancellationToken);
            return;
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
            await _botClient.SendMessage(
                message.Chat.Id,
                "Пожалуйста, отправьте файл",
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

                await _botClient.SendMessage(
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
                await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);

                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Файл успешно прикреплён",
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _botClient.SendMessage(
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

                        await _botClient.SendMessage(
                            message.Chat.Id,
                            "Сохранено",
                            cancellationToken: cancellationToken);
                        return;
                    }
                    else
                    {
                        await _botClient.SendMessage(
                            message.Chat.Id,
                            "Не удалось распознать формат. Используйте:\nИмя\nдд MMMM [YYYY]\n[контакт]\n\nИли введите имя для пошагового ввода",
                            cancellationToken: cancellationToken);
                        return;
                    }
                }

                data.PersonName = text;
                await UpdateStateAsync(state, BirthdayConversationSteps.Date, data, now, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Введите дату (например, 7 января)",
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.Date:
                if (!_birthdayDateParser.TryParseMonthDay(text, out var day, out var month))
                {
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Не удалось распознать дату. Формат: 7 января",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Day = day;
                data.Month = month;
                await UpdateStateAsync(state, BirthdayConversationSteps.BirthYear, data, now, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Введите год рождения или 'пропустить'",
                    replyMarkup: CreateSkipKeyboard(ConversationNames.BirthdayAdd, BirthdayConversationSteps.BirthYear),
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.BirthYear:
                if (IsSkip(text))
                {
                    data.BirthYear = null;
                }
                else if (!int.TryParse(text, out var birthYear) || birthYear <= 0)
                {
                    await _botClient.SendMessage(
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
                await _botClient.SendMessage(
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
                await _botClient.SendMessage(
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
            await _botClient.SendMessage(
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

        await _botClient.SendMessage(
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
                        await _botClient.SendMessage(
                            message.Chat.Id,
                            "Не удалось распознать формат. Используйте:\nНазвание\nдата/время [разовое|ежегодное]\n[описание]\n[место]\n[ссылка]\n\nМожно использовать маркеры: место:, ссылка:, описание:\nСсылки определяются автоматически\n\nИли введите название для пошагового ввода",
                            cancellationToken: cancellationToken);
                        return;
                    }
                }

                data.Title = text;
                await UpdateEventStateAsync(state, EventConversationSteps.Date, data, now, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Введите дату (например, 7 января 2026 или 2026-01-07)",
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Date:
                if (!_dateTimeParser.TryParse(text, out var parseResult) || parseResult is null)
                {
                    await _botClient.SendMessage(
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
                    await UpdateEventStateAsync(state, EventConversationSteps.Kind, data, now, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Выберите тип события: 'разовое' или 'ежегодное'",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await UpdateEventStateAsync(state, EventConversationSteps.AllDay, data, now, cancellationToken);
                    await _botClient.SendMessage(
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
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Введите время (HH:mm)",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await UpdateEventStateAsync(state, EventConversationSteps.Kind, data, now, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Выберите тип события: 'разовое' или 'ежегодное'",
                        cancellationToken: cancellationToken);
                }
                break;

            case EventConversationSteps.Time:
                if (!_dateTimeParser.TryParse(text, out var timeResult) || timeResult is null ||
                    !timeResult.Hour.HasValue || !timeResult.Minute.HasValue)
                {
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Не удалось распознать время. Формат: HH:mm",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Hour = timeResult.Hour;
                data.Minute = timeResult.Minute;
                await UpdateEventStateAsync(state, EventConversationSteps.Kind, data, now, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Выберите тип события: 'разовое' или 'ежегодное'",
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Kind:
                var kindText = text.ToLowerInvariant();
                if (kindText.Contains("разов") || kindText.Contains("one"))
                {
                    data.Kind = "OneOff";
                }
                else if (kindText.Contains("ежегод") || kindText.Contains("yearly"))
                {
                    data.Kind = "Yearly";
                }
                else
                {
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Введите 'разовое' или 'ежегодное'",
                        cancellationToken: cancellationToken);
                    return;
                }

                await UpdateEventStateAsync(state, EventConversationSteps.Description, data, now, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Введите описание или 'пропустить'",
                    replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.Description),
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Description:
                data.Description = IsSkip(text) ? null : text;
                await UpdateEventStateAsync(state, EventConversationSteps.Place, data, now, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Введите место или 'пропустить'",
                    replyMarkup: CreateSkipKeyboard(ConversationNames.EventAdd, EventConversationSteps.Place),
                    cancellationToken: cancellationToken);
                break;

            case EventConversationSteps.Place:
                data.Place = IsSkip(text) ? null : text;
                await UpdateEventStateAsync(state, EventConversationSteps.Link, data, now, cancellationToken);
                await _botClient.SendMessage(
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
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Состояние диалога сброшено. Начните заново с /event_add",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task SaveEventAsync(Message message, EventConversationData data, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(data.Title) || data.Month is null || data.Day is null || data.Kind is null)
        {
            await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
            await _botClient.SendMessage(
                message.Chat.Id,
                "Не удалось сохранить событие. Начните заново с /event_add",
                cancellationToken: cancellationToken);
            return;
        }

        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var now = _clock.UtcNow;
        var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);

        CreateEventCommand command;

        if (data.Kind == "OneOff")
        {
            if (data.Year is null || data.Month is null || data.Day is null)
            {
                await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Не удалось сохранить событие. Начните заново с /event_add",
                    cancellationToken: cancellationToken);
                return;
            }

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

            command = new CreateEventCommand(
                data.Title,
                EventKind.OneOff,
                data.IsAllDay == true,
                occursAtUtc,
                null,
                null,
                null,
                data.Description,
                data.Place,
                data.Link);
        }
        else
        {
            TimeSpan? timeOfDay = null;
            if (!data.IsAllDay!.Value && data.Hour.HasValue && data.Minute.HasValue)
            {
                timeOfDay = new TimeSpan(data.Hour.Value, data.Minute.Value, 0);
            }

            command = new CreateEventCommand(
                data.Title,
                EventKind.Yearly,
                data.IsAllDay == true,
                null,
                data.Month,
                data.Day,
                timeOfDay,
                data.Description,
                data.Place,
                data.Link);
        }

        await _createEvent.ExecuteAsync(command, cancellationToken);
        await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);

        await _botClient.SendMessage(
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
            await _botClient.SendMessage(
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
            await _botClient.SendMessage(
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
                    command = new UpdateEventCommand(eventId, "title", text, null, null, null, null, null, null, null, null);
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Название обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "description":
                    var description = IsSkip(text) ? null : text;
                    command = new UpdateEventCommand(eventId, "description", null, description, null, null, null, null, null, null, null);
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Описание обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "place":
                    var place = IsSkip(text) ? null : text;
                    command = new UpdateEventCommand(eventId, "place", null, null, place, null, null, null, null, null, null);
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Место обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "link":
                    var link = IsSkip(text) ? null : text;
                    command = new UpdateEventCommand(eventId, "link", null, null, null, link, null, null, null, null, null);
                    await _updateEvent.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Ссылка обновлена",
                        cancellationToken: cancellationToken);
                    break;

                case "date":
                    if (!_dateTimeParser.TryParse(text, out var parseResult) || parseResult is null)
                    {
                        await _botClient.SendMessage(
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
                        await _botClient.SendMessage(
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
                        await _botClient.SendMessage(
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
                        await _botClient.SendMessage(
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

                default:
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Неизвестное поле для редактирования",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            await _botClient.SendMessage(
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

            var command = new UpdateEventCommand(
                eventId,
                "occursAtUtc",
                null,
                null,
                null,
                null,
                occursAtUtc,
                null,
                null,
                null,
                data.IsAllDay);
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

            var command = new UpdateEventCommand(
                eventId,
                "yearlyDate",
                null,
                null,
                null,
                null,
                null,
                data.Month,
                data.Day,
                timeOfDay,
                data.IsAllDay);
            await _updateEvent.ExecuteAsync(command, cancellationToken);
        }

        await _conversationRepository.DeleteAsync(userId, cancellationToken);
        await _botClient.SendMessage(
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
            await _botClient.SendMessage(
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
            await _botClient.SendMessage(
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
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Имя обновлено",
                        cancellationToken: cancellationToken);
                    break;

                case "date":
                    if (!_birthdayDateParser.TryParseMonthDay(text, out var day, out var month))
                    {
                        await _botClient.SendMessage(
                            message.Chat.Id,
                            "Не удалось распознать дату. Формат: 7 января",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    command = new UpdateBirthdayCommand(birthdayId, "date", null, day, month, null, null);
                    await _updateBirthday.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
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
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Год рождения обновлён",
                        cancellationToken: cancellationToken);
                    break;

                case "contact":
                    var contact = IsSkip(text) ? null : text;
                    command = new UpdateBirthdayCommand(birthdayId, "contact", null, null, null, null, contact);
                    await _updateBirthday.ExecuteAsync(command, cancellationToken);
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Контакт обновлён",
                        cancellationToken: cancellationToken);
                    break;

                default:
                    await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Неизвестное поле для редактирования",
                        cancellationToken: cancellationToken);
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                $"Ошибка: {ex.Message}",
                cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
            await _botClient.SendMessage(
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
        var parts = dateLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
        {
            var lastPart = parts[^1];
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
        if (string.IsNullOrWhiteSpace(data.Title) || data.Month is null || data.Day is null || data.Kind is null)
        {
            await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
            await _botClient.SendMessage(
                message.Chat.Id,
                "Не удалось сохранить событие. Проверьте формат",
                cancellationToken: cancellationToken);
            return;
        }

        var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
        var now = _clock.UtcNow;

        CreateEventCommand command;

        if (data.Kind == "OneOff")
        {
            if (data.Year is null || data.Month is null || data.Day is null)
            {
                await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    "Для разового события требуется указать год",
                    cancellationToken: cancellationToken);
                return;
            }

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

            command = new CreateEventCommand(
                data.Title,
                EventKind.OneOff,
                data.IsAllDay == true,
                occursAtUtc,
                null,
                null,
                null,
                data.Description,
                data.Place,
                data.Link);
        }
        else
        {
            TimeSpan? timeOfDay = null;
            if (!data.IsAllDay!.Value && data.Hour.HasValue && data.Minute.HasValue)
            {
                timeOfDay = new TimeSpan(data.Hour.Value, data.Minute.Value, 0);
            }

            command = new CreateEventCommand(
                data.Title,
                EventKind.Yearly,
                data.IsAllDay == true,
                null,
                data.Month,
                data.Day,
                timeOfDay,
                data.Description,
                data.Place,
                data.Link);
        }

        await _createEvent.ExecuteAsync(command, cancellationToken);
        await _conversationRepository.DeleteAsync(message.From!.Id, cancellationToken);

        await _botClient.SendMessage(
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

    private static bool IsSkip(string text)
    {
        return text.Equals("skip", StringComparison.OrdinalIgnoreCase)
               || text.Equals("пропустить", StringComparison.OrdinalIgnoreCase)
               || text.Equals("-", StringComparison.OrdinalIgnoreCase);
    }
}
