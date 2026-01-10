using SnakeFrogCalendarBot.Application.Abstractions.Time;

namespace SnakeFrogCalendarBot.Infrastructure.Time;

public sealed class EnvTimeZoneProvider : ITimeZoneProvider
{
    private readonly string _timeZoneId;

    public EnvTimeZoneProvider(string timeZoneId)
    {
        _timeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? "Europe/Moscow" : timeZoneId;
    }

    public string GetTimeZoneId()
    {
        return _timeZoneId;
    }
}