using SnakeFrogCalendarBot.Application.Abstractions.Telegram;

namespace SnakeFrogCalendarBot.Application.UseCases.Notifications;

public sealed class SendDigest
{
    private readonly ITelegramPublisher _telegramPublisher;

    public SendDigest(ITelegramPublisher telegramPublisher)
    {
        _telegramPublisher = telegramPublisher;
    }

    public async Task<int> ExecuteAsync(string digestText, CancellationToken cancellationToken)
    {
        return await _telegramPublisher.SendMessageAsync(digestText, cancellationToken);
    }
}
