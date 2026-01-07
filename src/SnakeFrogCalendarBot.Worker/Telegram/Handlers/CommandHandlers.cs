using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Domain.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SnakeFrogCalendarBot.Worker.Telegram.Handlers;

public sealed class CommandHandlers
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConversationStateRepository _conversationRepository;
    private readonly IClock _clock;
    private readonly ListBirthdays _listBirthdays;
    private readonly BirthdayListFormatter _formatter;

    public CommandHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IClock clock,
        ListBirthdays listBirthdays,
        BirthdayListFormatter formatter)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _clock = clock;
        _listBirthdays = listBirthdays;
        _formatter = formatter;
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
            default:
                await _botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Неизвестная команда. Доступные: /birthday_add, /birthday_list",
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
        var text = _formatter.Format(birthdays);

        await _botClient.SendTextMessageAsync(
            message.Chat.Id,
            text,
            cancellationToken: cancellationToken);
    }
}
