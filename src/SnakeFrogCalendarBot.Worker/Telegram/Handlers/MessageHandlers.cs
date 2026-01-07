using System.Text.Json;
using System.Text.Json.Serialization;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Domain.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;

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
    private readonly IBirthdayDateParser _dateParser;
    private readonly CreateBirthday _createBirthday;
    private readonly IClock _clock;

    public MessageHandlers(
        ITelegramBotClient botClient,
        IConversationStateRepository conversationRepository,
        IBirthdayDateParser dateParser,
        CreateBirthday createBirthday,
        IClock clock)
    {
        _botClient = botClient;
        _conversationRepository = conversationRepository;
        _dateParser = dateParser;
        _createBirthday = createBirthday;
        _clock = clock;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        if (message.From?.Id is not { } userId || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var state = await _conversationRepository.GetByUserIdAsync(userId, cancellationToken);
        if (state is null)
        {
            await _botClient.SendTextMessageAsync(
                message.Chat.Id,
                "Используйте /birthday_add или /birthday_list",
                cancellationToken: cancellationToken);
            return;
        }

        if (!string.Equals(state.ConversationName, ConversationNames.BirthdayAdd, StringComparison.OrdinalIgnoreCase))
        {
            await _conversationRepository.DeleteAsync(userId, cancellationToken);
            await _botClient.SendTextMessageAsync(
                message.Chat.Id,
                "Состояние диалога сброшено. Начните заново с /birthday_add",
                cancellationToken: cancellationToken);
            return;
        }

        await HandleBirthdayAddAsync(message, state, cancellationToken);
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
                data.PersonName = text;
                await UpdateStateAsync(state, BirthdayConversationSteps.Date, data, now, cancellationToken);
                await _botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Введите дату (например, 7 января)",
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.Date:
                if (!_dateParser.TryParseMonthDay(text, out var day, out var month))
                {
                    await _botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Не удалось распознать дату. Формат: 7 января",
                        cancellationToken: cancellationToken);
                    return;
                }

                data.Day = day;
                data.Month = month;
                await UpdateStateAsync(state, BirthdayConversationSteps.BirthYear, data, now, cancellationToken);
                await _botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Введите год рождения или 'пропустить'",
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.BirthYear:
                if (IsSkip(text))
                {
                    data.BirthYear = null;
                }
                else if (!int.TryParse(text, out var birthYear) || birthYear <= 0)
                {
                    await _botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        "Введите год рождения или 'пропустить'",
                        cancellationToken: cancellationToken);
                    return;
                }
                else
                {
                    data.BirthYear = birthYear;
                }

                await UpdateStateAsync(state, BirthdayConversationSteps.Contact, data, now, cancellationToken);
                await _botClient.SendTextMessageAsync(
                    message.Chat.Id,
                    "Введите контакт или 'пропустить'",
                    cancellationToken: cancellationToken);
                break;

            case BirthdayConversationSteps.Contact:
                data.Contact = IsSkip(text) ? null : text;
                await SaveBirthdayAsync(message, data, cancellationToken);
                break;

            default:
                await _conversationRepository.DeleteAsync(state.UserId, cancellationToken);
                await _botClient.SendTextMessageAsync(
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
            await _botClient.SendTextMessageAsync(
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

        await _botClient.SendTextMessageAsync(
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

    private static bool IsSkip(string text)
    {
        return text.Equals("skip", StringComparison.OrdinalIgnoreCase)
               || text.Equals("пропустить", StringComparison.OrdinalIgnoreCase)
               || text.Equals("-", StringComparison.OrdinalIgnoreCase);
    }
}
