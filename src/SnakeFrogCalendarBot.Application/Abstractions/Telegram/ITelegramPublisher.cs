namespace SnakeFrogCalendarBot.Application.Abstractions.Telegram;

public interface ITelegramPublisher
{
    Task<int> SendMessageAsync(string text, CancellationToken cancellationToken);
    Task EditMessageAsync(int messageId, string text, CancellationToken cancellationToken);
}
