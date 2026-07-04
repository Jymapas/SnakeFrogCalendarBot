using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace SnakeFrogCalendarBot.Worker.Telegram;

internal static class BotClientExtensions
{
    private static readonly LinkPreviewOptions NoLinkPreview = new() { IsDisabled = true };

    internal static Task<Message> SendNoPreview(
        this ITelegramBotClient client,
        ChatId chatId,
        string text,
        ReplyMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
        => client.SendMessage(chatId, text,
            replyMarkup: replyMarkup,
            linkPreviewOptions: NoLinkPreview,
            cancellationToken: cancellationToken);

    internal static Task<Message> EditNoPreview(
        this ITelegramBotClient client,
        ChatId chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
        => client.EditMessageText(chatId, messageId, text,
            replyMarkup: replyMarkup,
            linkPreviewOptions: NoLinkPreview,
            cancellationToken: cancellationToken);
}
