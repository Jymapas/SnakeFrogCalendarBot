using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Domain.Entities;
using SnakeFrogCalendarBot.Worker.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram.Handlers;

public sealed class CommandHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversationStateRepository _conversationRepository;
    private readonly IClock _clock;
    private readonly ListBirthdays _listBirthdays;
    private readonly BirthdayListFormatter _birthdayFormatter;
    private readonly ListUpcomingItems _listUpcomingItems;
    private readonly EventListFormatter _eventFormatter;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly BuildDailyDigest _buildDailyDigest;
    private readonly BuildWeeklyDigest _buildWeeklyDigest;
    private readonly BuildMonthlyDigest _buildMonthlyDigest;
    private readonly DigestFormatter _digestFormatter;
    private readonly SendDigest _sendDigest;
    private readonly IEventRepository _eventRepository;
    private readonly IBirthdayRepository _birthdayRepository;

    public CommandHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IClock clock,
        ListBirthdays listBirthdays,
        BirthdayListFormatter birthdayFormatter,
        ListUpcomingItems listUpcomingItems,
        EventListFormatter eventFormatter,
        IAttachmentRepository attachmentRepository,
        BuildDailyDigest buildDailyDigest,
        BuildWeeklyDigest buildWeeklyDigest,
        BuildMonthlyDigest buildMonthlyDigest,
        DigestFormatter digestFormatter,
        SendDigest sendDigest,
        IEventRepository eventRepository,
        IBirthdayRepository birthdayRepository)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _clock = clock;
        _listBirthdays = listBirthdays;
        _birthdayFormatter = birthdayFormatter;
        _listUpcomingItems = listUpcomingItems;
        _eventFormatter = eventFormatter;
        _attachmentRepository = attachmentRepository;
        _buildDailyDigest = buildDailyDigest;
        _buildWeeklyDigest = buildWeeklyDigest;
        _buildMonthlyDigest = buildMonthlyDigest;
        _digestFormatter = digestFormatter;
        _sendDigest = sendDigest;
        _eventRepository = eventRepository;
        _birthdayRepository = birthdayRepository;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var command = message.Text.Split(' ', '\n')[0].Trim();
        var atIndex = command.IndexOf('@');
        if (atIndex > 0)
        {
            command = command[..atIndex];
        }

        switch (command)
        {
            case BotCommands.BirthdayAdd:
                await StartBirthdayAddAsync(message, cancellationToken);
                break;
            case BotCommands.BirthdayList:
                await SendBirthdayListAsync(message, cancellationToken);
                break;
            case BotCommands.EventAdd:
                await StartEventAddAsync(message, cancellationToken);
                break;
            case BotCommands.EventList:
                await SendEventListAsync(message, cancellationToken);
                break;
            case BotCommands.EventEdit:
                await SendEventListForEditAsync(message, cancellationToken);
                break;
            case BotCommands.EventDelete:
                await SendEventListForDeleteAsync(message, cancellationToken);
                break;
            case BotCommands.BirthdayEdit:
                await SendBirthdayListForEditAsync(message, cancellationToken);
                break;
            case BotCommands.BirthdayDelete:
                await SendBirthdayListForDeleteAsync(message, cancellationToken);
                break;
            case BotCommands.Cancel:
                await CancelConversationAsync(message, cancellationToken);
                break;
            case BotCommands.DigestTest:
                await TestDigestAsync(message, cancellationToken);
                break;
            case BotCommands.Start:
            case BotCommands.Menu:
                await ShowMainMenuAsync(message, cancellationToken);
                break;
            default:
                var availableCommands = string.Join(", ", BotCommands.All);
                await _botClient.SendMessage(
                    message.Chat.Id,
                    $"Неизвестная команда. Доступные: {availableCommands}",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task StartBirthdayAddAsync(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id;
        if (userId is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var state = new ConversationState(
            userId.Value,
            ConversationNames.BirthdayAdd,
            BirthdayConversationSteps.Name,
            null,
            now);

        await _conversationRepository.UpsertAsync(state, cancellationToken);
        await _botClient.SendMessage(
            message.Chat.Id,
            "Введите имя\n\nИли отправьте многострочное сообщение:\nИмя\nдд MMMM [YYYY]\n[контакт]",
            cancellationToken: cancellationToken);
    }

    private async Task SendBirthdayListAsync(Message message, CancellationToken cancellationToken)
    {
        // Показываем выбор месяца вместо полного списка
        await _botClient.SendMessage(
            message.Chat.Id,
            "Выберите месяц:",
            replyMarkup: InlineKeyboards.MonthSelectionKeyboard(),
            cancellationToken: cancellationToken);
    }

    private async Task SendBirthdayListForEditAsync(Message message, CancellationToken cancellationToken)
    {
        var birthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        
        if (birthdays.Count == 0)
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                "Дней рождения пока нет",
                cancellationToken: cancellationToken);
            return;
        }

        await _botClient.SendMessage(
            message.Chat.Id,
            "Выберите месяц для редактирования дня рождения:",
            replyMarkup: InlineKeyboards.MonthSelectionKeyboardForEdit(),
            cancellationToken: cancellationToken);
    }

    private async Task SendBirthdayListForDeleteAsync(Message message, CancellationToken cancellationToken)
    {
        var birthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        var text = "Выберите день рождения для удаления:";
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var birthday in birthdays)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"🗑 {birthday.PersonName}", $"birthday_delete:{birthday.Id}")
            });
        }

        if (buttons.Count == 0)
        {
            text = "Дней рождения пока нет";
        }

        await _botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: buttons.Count > 0 ? new InlineKeyboardMarkup(buttons) : null,
            cancellationToken: cancellationToken);
    }

    private async Task StartEventAddAsync(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id;
        if (userId is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var state = new ConversationState(
            userId.Value,
            ConversationNames.EventAdd,
            EventConversationSteps.Title,
            null,
            now);

        await _conversationRepository.UpsertAsync(state, cancellationToken);
        await _botClient.SendMessage(
            message.Chat.Id,
            "Введите название события\n\nИли отправьте многострочное сообщение:\nНазвание\nдата/время [разовое|ежегодное]\n[описание]\n[место]\n[ссылка]\n\nМожно использовать маркеры: место:, ссылка:, описание:\nСсылки определяются автоматически",
            cancellationToken: cancellationToken);
    }

    private async Task SendEventListAsync(Message message, CancellationToken cancellationToken)
    {
        var events = await _listUpcomingItems.ExecuteAsync(cancellationToken);
        
        var eventAttachments = new Dictionary<int, Attachment?>();
        foreach (var eventEntity in events)
        {
            var currentAttachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventEntity.Id, cancellationToken);
            eventAttachments[eventEntity.Id] = currentAttachment;
        }

        var text = _eventFormatter.Format(events, eventAttachments);
        var inlineKeyboard = await CreateEventListInlineKeyboard(events, cancellationToken);

        await _botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task<InlineKeyboardMarkup> CreateEventListInlineKeyboard(
        IReadOnlyList<Event> events,
        CancellationToken cancellationToken)
    {
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var eventEntity in events)
        {
            var currentAttachment = await _attachmentRepository.GetCurrentByEventIdAsync(eventEntity.Id, cancellationToken);
            var hasFile = currentAttachment != null;

            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("📎 Добавить файл", $"event_attach:{eventEntity.Id}")
            };

            if (hasFile)
            {
                row.Add(InlineKeyboardButton.WithCallbackData("📥 Скачать файл", $"event_download_file:{eventEntity.Id}"));
                row.Add(InlineKeyboardButton.WithCallbackData("♻️ Заменить файл", $"event_replace_file:{eventEntity.Id}"));
            }

            buttons.Add(row);
        }

        return new InlineKeyboardMarkup(buttons);
    }

    private async Task SendEventListForEditAsync(Message message, CancellationToken cancellationToken)
    {
        var events = await _eventRepository.ListAllAsync(cancellationToken);
        var text = "Выберите событие для редактирования:";
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var eventEntity in events)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"✏️ {eventEntity.Title}", $"event_edit:{eventEntity.Id}")
            });
        }

        if (buttons.Count == 0)
        {
            text = "Событий пока нет";
        }

        await _botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: buttons.Count > 0 ? new InlineKeyboardMarkup(buttons) : null,
            cancellationToken: cancellationToken);
    }

    private async Task SendEventListForDeleteAsync(Message message, CancellationToken cancellationToken)
    {
        var events = await _eventRepository.ListAllAsync(cancellationToken);
        var text = "Выберите событие для удаления:";
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var eventEntity in events)
        {
            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"🗑 {eventEntity.Title}", $"event_delete:{eventEntity.Id}")
            });
        }

        if (buttons.Count == 0)
        {
            text = "Событий пока нет";
        }

        await _botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: buttons.Count > 0 ? new InlineKeyboardMarkup(buttons) : null,
            cancellationToken: cancellationToken);
    }

    private async Task CancelConversationAsync(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id;
        if (userId is null)
        {
            return;
        }

        await _conversationRepository.DeleteAsync(userId.Value, cancellationToken);
        await _botClient.SendMessage(
            message.Chat.Id,
            "Действие отменено",
            cancellationToken: cancellationToken);
    }

    private async Task TestDigestAsync(Message message, CancellationToken cancellationToken)
    {
        var parts = message.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is null || parts.Length < 2)
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                "Использование: /digest_test daily|weekly|monthly",
                cancellationToken: cancellationToken);
            return;
        }

        var digestType = parts[1].ToLowerInvariant();
        string digestText;

        try
        {
            switch (digestType)
            {
                case "daily":
                    var dailyResult = await _buildDailyDigest.ExecuteAsync(cancellationToken);
                    digestText = _digestFormatter.FormatDaily(dailyResult.Date, dailyResult.Items);
                    break;

                case "weekly":
                    var weeklyResult = await _buildWeeklyDigest.ExecuteAsync(cancellationToken);
                    digestText = _digestFormatter.FormatWeekly(weeklyResult.PeriodStart, weeklyResult.PeriodEnd, weeklyResult.Items);
                    break;

                case "monthly":
                    var monthlyResult = await _buildMonthlyDigest.ExecuteAsync(cancellationToken);
                    digestText = _digestFormatter.FormatMonthly(monthlyResult.PeriodStart, monthlyResult.PeriodEnd, monthlyResult.Items);
                    break;

                default:
                    await _botClient.SendMessage(
                        message.Chat.Id,
                        "Неизвестный тип дайджеста. Используйте: daily, weekly или monthly",
                        cancellationToken: cancellationToken);
                    return;
            }

            await _botClient.SendMessage(
                message.Chat.Id,
                digestText,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            await _botClient.SendMessage(
                message.Chat.Id,
                $"Ошибка при формировании дайджеста: {ex.Message}",
                cancellationToken: cancellationToken);
        }
    }

    private async Task ShowMainMenuAsync(Message message, CancellationToken cancellationToken)
    {
        var isStart = message.Text?.Trim() == BotCommands.Start;
        var text = isStart 
            ? "Добро пожаловать! Используйте клавиатуру для быстрого доступа к функциям."
            : "Используйте клавиатуру для быстрого доступа к функциям.";
        
        await _botClient.SendMessage(
            message.Chat.Id,
            text,
            replyMarkup: ReplyKeyboards.MainKeyboard(),
            cancellationToken: cancellationToken);
    }
}
