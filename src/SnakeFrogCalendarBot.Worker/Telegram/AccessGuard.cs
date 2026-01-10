using SnakeFrogCalendarBot.Worker.Config;

namespace SnakeFrogCalendarBot.Worker.Telegram;

public sealed class AccessGuard
{
    private readonly HashSet<long> _allowedUserIds;

    public AccessGuard(AppOptions options)
    {
        _allowedUserIds = new HashSet<long>(options.AllowedUserIds);
    }

    public bool IsAllowed(long userId) => _allowedUserIds.Contains(userId);
}
