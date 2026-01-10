using System.Globalization;
using NodaTime;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Time;

namespace SnakeFrogCalendarBot.Infrastructure.Parsing;

public sealed class RuDateTimeParser : IDateTimeParser
{
    private readonly IClock _clock;
    private readonly ITimeZoneProvider _timeZoneProvider;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly string[] DateTimeFormats =
    {
        "yyyy-MM-dd HH:mm",
        "d MMMM yyyy HH:mm"
    };

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd",
        "d MMMM yyyy",
        "d MMMM"
    };

    private static readonly string[] TimeFormats =
    {
        "HH:mm"
    };

    public RuDateTimeParser(IClock clock, ITimeZoneProvider timeZoneProvider)
    {
        _clock = clock;
        _timeZoneProvider = timeZoneProvider;
    }

    public bool TryParse(string input, out DateTimeParseResult? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        if (TryParseDateTime(trimmed, out var dateTimeResult))
        {
            result = dateTimeResult;
            return true;
        }

        if (TryParseDate(trimmed, out var dateResult))
        {
            result = dateResult;
            return true;
        }

        if (TryParseTime(trimmed, out var timeResult))
        {
            result = timeResult;
            return true;
        }

        return false;
    }

    private bool TryParseDateTime(string input, out DateTimeParseResult? result)
    {
        result = null;

        if (DateTime.TryParseExact(
                input,
                DateTimeFormats,
                _culture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            var hasYear = input.Contains("yyyy", StringComparison.OrdinalIgnoreCase) ||
                          DateTimeFormats.Take(2).Any(f => f.Contains("yyyy") && input.Contains(parsed.Year.ToString()));

            if (!hasYear)
            {
                var now = _clock.UtcNow;
                var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
                var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
                var localDate = nowInZone.Date;

                var candidateDate = new LocalDate(localDate.Year, parsed.Month, parsed.Day);
                if (candidateDate < localDate)
                {
                    candidateDate = candidateDate.PlusYears(1);
                }

                result = new DateTimeParseResult(
                    candidateDate.Year,
                    candidateDate.Month,
                    candidateDate.Day,
                    parsed.Hour,
                    parsed.Minute,
                    false);
            }
            else
            {
                result = new DateTimeParseResult(
                    parsed.Year,
                    parsed.Month,
                    parsed.Day,
                    parsed.Hour,
                    parsed.Minute,
                    true);
            }

            return true;
        }

        return false;
    }

    private bool TryParseDate(string input, out DateTimeParseResult? result)
    {
        result = null;

        if (DateTime.TryParseExact(
                input,
                DateFormats,
                _culture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            var hasYear = input.Contains("yyyy", StringComparison.OrdinalIgnoreCase) ||
                          DateFormats.Take(2).Any(f => f.Contains("yyyy") && input.Contains(parsed.Year.ToString()));

            if (!hasYear)
            {
                var now = _clock.UtcNow;
                var timeZone = DateTimeZoneProviders.Tzdb[_timeZoneProvider.GetTimeZoneId()];
                var nowInZone = Instant.FromDateTimeUtc(now).InZone(timeZone);
                var localDate = nowInZone.Date;

                var candidateDate = new LocalDate(localDate.Year, parsed.Month, parsed.Day);
                if (candidateDate < localDate)
                {
                    candidateDate = candidateDate.PlusYears(1);
                }

                result = new DateTimeParseResult(
                    candidateDate.Year,
                    candidateDate.Month,
                    candidateDate.Day,
                    null,
                    null,
                    false);
            }
            else
            {
                result = new DateTimeParseResult(
                    parsed.Year,
                    parsed.Month,
                    parsed.Day,
                    null,
                    null,
                    true);
            }

            return true;
        }

        return false;
    }

    private bool TryParseTime(string input, out DateTimeParseResult? result)
    {
        result = null;

        if (TimeSpan.TryParseExact(input, TimeFormats, _culture, out var timeSpan))
        {
            result = new DateTimeParseResult(
                0,
                0,
                0,
                timeSpan.Hours,
                timeSpan.Minutes,
                false);
            return true;
        }

        return false;
    }
}