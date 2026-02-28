using System.Collections.Concurrent;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public sealed class PinnedMessageCleanupRegistry : IPinnedMessageCleanupRegistry
{
    private readonly ConcurrentDictionary<int, byte> _pendingPinnedMessages = new();

    public void RegisterPinnedMessage(int pinnedMessageId)
    {
        _pendingPinnedMessages[pinnedMessageId] = 0;
    }

    public bool TryConsumePinnedMessage(int pinnedMessageId)
    {
        return _pendingPinnedMessages.TryRemove(pinnedMessageId, out _);
    }
}
