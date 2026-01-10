using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Formatting;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class SendDigest
{
    private readonly ITelegramPublisher _telegramPublisher;
    private readonly DigestFormatter _formatter;

    public SendDigest(
        ITelegramPublisher telegramPublisher,
        DigestFormatter formatter)
    {
        _telegramPublisher = telegramPublisher;
        _formatter = formatter;
    }

    public async Task ExecuteAsync(string digestText, CancellationToken cancellationToken)
    {
        await _telegramPublisher.SendMessageAsync(digestText, cancellationToken);
    }
}