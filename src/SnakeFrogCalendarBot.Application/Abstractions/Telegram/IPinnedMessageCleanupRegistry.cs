namespace SnakeFrogCalendarBot.Application.Abstractions.Telegram;

public interface IPinnedMessageCleanupRegistry
{
    void RegisterPinnedMessage(int pinnedMessageId);
    bool TryConsumePinnedMessage(int pinnedMessageId);
}
