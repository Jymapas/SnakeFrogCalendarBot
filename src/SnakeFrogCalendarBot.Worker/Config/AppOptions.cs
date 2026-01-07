using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace SnakeFrogCalendarBot.Worker.Config;

public sealed class AppOptions
{
    public string TelegramBotToken { get; init; } = string.Empty;
    public IReadOnlyList<long> AllowedUserIds { get; init; } = Array.Empty<long>();
    public string TelegramTargetChat { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public string PostgresConnectionString { get; init; } = string.Empty;

    public static AppOptions FromConfiguration(IConfiguration configuration)
    {
        var token = configuration["TELEGRAM_BOT_TOKEN"]?.Trim();
        var allowedIdsRaw = configuration["TELEGRAM_ALLOWED_USER_IDS"];
        var targetChat = configuration["TELEGRAM_TARGET_CHAT"]?.Trim();
        var timeZone = configuration["TZ"]?.Trim();
        var connectionString = configuration["POSTGRES_CONNECTION_STRING"]?.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("TELEGRAM_BOT_TOKEN is required.");
        }

        if (string.IsNullOrWhiteSpace(allowedIdsRaw))
        {
            throw new InvalidOperationException("TELEGRAM_ALLOWED_USER_IDS is required.");
        }

        var allowedIds = allowedIdsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value =>
            {
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    throw new InvalidOperationException("TELEGRAM_ALLOWED_USER_IDS must contain valid numeric ids.");
                }

                return parsed;
            })
            .ToArray();

        if (allowedIds.Length == 0)
        {
            throw new InvalidOperationException("TELEGRAM_ALLOWED_USER_IDS must contain at least one id.");
        }

        if (string.IsNullOrWhiteSpace(targetChat))
        {
            throw new InvalidOperationException("TELEGRAM_TARGET_CHAT is required.");
        }

        if (string.IsNullOrWhiteSpace(timeZone))
        {
            throw new InvalidOperationException("TZ is required.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
        }

        return new AppOptions
        {
            TelegramBotToken = token,
            AllowedUserIds = allowedIds,
            TelegramTargetChat = targetChat,
            TimeZone = timeZone,
            PostgresConnectionString = connectionString
        };
    }
}
