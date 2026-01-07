using SnakeFrogCalendarBot.Application.Abstractions.Time;

namespace SnakeFrogCalendarBot.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
