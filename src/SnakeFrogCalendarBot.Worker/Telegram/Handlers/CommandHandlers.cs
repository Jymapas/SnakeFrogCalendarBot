using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Domain.Entities;
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

    public CommandHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IClock clock,
        ListBirthdays listBirthdays,
        BirthdayListFormatter birthdayFormatter,
        ListUpcomingItems listUpcomingItems,
        EventListFormatter eventFormatter,
        IAttachmentRepository attachmentRepository)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _clock = clock;
        _listBirthdays = listBirthdays;
        _birthdayFormatter = birthdayFormatter;
        _listUpcomingItems = listUpcomingItems;
        _eventFormatter = eventFormatter;
        _attachmentRepository = attachmentRepository;
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
            case "/birthday_add":
                await StartBirthdayAddAsync(message, cancellationToken);
                break;
            case "/birthday_list":
                await SendBirthdayListAsync(message, cancellationToken);
                break;
            case "/event_add":
                await StartEventAddAsync(message, cancellationToken);
                break;
            case "/event_list":
                await SendEventListAsync(message, cancellationToken);
                break;
            case "/cancel":
                await CancelConversationAsync(message, cancellationToken);
                break;
            default:
                await _botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Неизвестная команда. Доступные: /birthday_add, /birthday_list, /event_add, /event_list, /cancel",
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
        await _botClient.SendTextMessageAsync(
            message.Chat.Id,
            "Введите имя",
            cancellationToken: cancellationToken);
    }

    private async Task SendBirthdayListAsync(Message message, CancellationToken cancellationToken)
    {
        var birthdays = await _listBirthdays.ExecuteAsync(cancellationToken);
        var text = _birthdayFormatter.Format(birthdays);

        await _botClient.SendTextMessageAsync(
            message.Chat.Id,
            text,
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
        await _botClient.SendTextMessageAsync(
            message.Chat.Id,
            "Введите название события",
            cancellationToken: cancellationToken);
    }

    private async Task SendEventListAsync(Message message, CancellationToken cancellationToken)
    {
        var events = await _listUpcomingItems.ExecuteAsync(cancellationToken);
        
        var eventAttachmentCount = new Dictionary<int, int>();
        foreach (var eventEntity in events)
        {
            var attachments = await _attachmentRepository.GetByEventIdAsync(eventEntity.Id, cancellationToken);
            eventAttachmentCount[eventEntity.Id] = attachments.Count;
        }

        var text = _eventFormatter.Format(events, eventAttachmentCount);
        var inlineKeyboard = await CreateEventListInlineKeyboard(events, cancellationToken);

        await _botClient.SendTextMessageAsync(
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
            var attachments = await _attachmentRepository.GetByEventIdAsync(eventEntity.Id, cancellationToken);
            var hasFiles = attachments.Count > 0;

            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("📎 Добавить файл", $"event_attach:{eventEntity.Id}")
            };

            if (hasFiles)
            {
                row.Add(InlineKeyboardButton.WithCallbackData("♻️ Заменить файл", $"event_replace_file:{eventEntity.Id}"));
            }

            buttons.Add(row);
        }

        return new InlineKeyboardMarkup(buttons);
    }

    private async Task CancelConversationAsync(Message message, CancellationToken cancellationToken)
    {
        var userId = message.From?.Id;
        if (userId is null)
        {
            return;
        }

        await _conversationRepository.DeleteAsync(userId.Value, cancellationToken);
        await _botClient.SendTextMessageAsync(
            message.Chat.Id,
            "Диалог отменён",
            cancellationToken: cancellationToken);
    }
}
