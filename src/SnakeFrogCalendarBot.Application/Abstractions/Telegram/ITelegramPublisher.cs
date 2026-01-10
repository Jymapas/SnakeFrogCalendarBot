namespace SnakeFrogCalendarBot.Application.Abstractions.Telegram;

public interface ITelegramPublisher
{
    Task SendMessageAsync(string text, CancellationToken cancellationToken);
}