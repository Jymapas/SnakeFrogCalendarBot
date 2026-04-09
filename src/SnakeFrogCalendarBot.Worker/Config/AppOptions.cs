using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace SnakeFrogCalendarBot.Worker.Config;

public sealed class AppOptions
{
    private static readonly TimeSpan DefaultTelegramChannelTriggerWindow = TimeSpan.FromHours(3);

    public string TelegramBotToken { get; init; } = string.Empty;
    public IReadOnlyList<long> AllowedUserIds { get; init; } = Array.Empty<long>();
    public string TelegramTargetChat { get; init; } = string.Empty;
    public string TimeZone { get; init; } = string.Empty;
    public string PostgresConnectionString { get; init; } = string.Empty;
    public TimeSpan TelegramChannelTriggerWindow { get; init; } = DefaultTelegramChannelTriggerWindow;
    public string MiniAppUrl { get; init; } = string.Empty;
    public string MiniAppAllowedOrigin { get; init; } = string.Empty;
    public string GitHubDeployToken { get; init; } = string.Empty;
    public string GitHubRepo { get; init; } = string.Empty;

    public static AppOptions FromConfiguration(IConfiguration configuration)
    {
        var token = configuration["TELEGRAM_BOT_TOKEN"]?.Trim();
        var allowedIdsRaw = configuration["TELEGRAM_ALLOWED_USER_IDS"];
        var targetChat = configuration["TELEGRAM_TARGET_CHAT"]?.Trim();
        var timeZone = configuration["TZ"]?.Trim();
        var dbHost = configuration["POSTGRES_HOST"]?.Trim();
        var dbPortRaw = configuration["POSTGRES_PORT"]?.Trim();
        var dbName = configuration["POSTGRES_DB"]?.Trim();
        var dbUser = configuration["POSTGRES_USER"]?.Trim();
        var dbPassword = configuration["POSTGRES_PASSWORD"]?.Trim();
        var triggerWindowRaw = configuration["TELEGRAM_CHANNEL_TRIGGER_WINDOW_MINUTES"]?.Trim();

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

        if (string.IsNullOrWhiteSpace(dbHost))
        {
            throw new InvalidOperationException("POSTGRES_HOST is required.");
        }

        if (string.IsNullOrWhiteSpace(dbName))
        {
            throw new InvalidOperationException("POSTGRES_DB is required.");
        }

        if (string.IsNullOrWhiteSpace(dbUser))
        {
            throw new InvalidOperationException("POSTGRES_USER is required.");
        }

        var port = 5432;
        if (!string.IsNullOrWhiteSpace(dbPortRaw)
            && !int.TryParse(dbPortRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
        {
            throw new InvalidOperationException("POSTGRES_PORT must be a valid number.");
        }

        if (IsRunningInDocker())
        {
            if (string.IsNullOrWhiteSpace(dbHost) || 
                dbHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                dbHost.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                dbHost = "postgres";
            }
        }
        else
        {
            if (dbHost == "postgres")
            {
                dbHost = "localhost";
            }
        }

        var connectionString = string.IsNullOrWhiteSpace(dbPassword)
            ? $"Host={dbHost};Port={port};Database={dbName};Username={dbUser}"
            : $"Host={dbHost};Port={port};Database={dbName};Username={dbUser};Password={dbPassword}";

        var triggerWindow = DefaultTelegramChannelTriggerWindow;
        if (!string.IsNullOrWhiteSpace(triggerWindowRaw))
        {
            if (!int.TryParse(triggerWindowRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                || minutes <= 0)
            {
                throw new InvalidOperationException(
                    "TELEGRAM_CHANNEL_TRIGGER_WINDOW_MINUTES must be a positive integer when provided.");
            }

            triggerWindow = TimeSpan.FromMinutes(minutes);
        }

        return new AppOptions
        {
            TelegramBotToken = token,
            AllowedUserIds = allowedIds,
            TelegramTargetChat = targetChat,
            TimeZone = timeZone,
            PostgresConnectionString = connectionString,
            TelegramChannelTriggerWindow = triggerWindow,
            MiniAppUrl = configuration["MINI_APP_URL"]?.Trim() ?? string.Empty,
            MiniAppAllowedOrigin = configuration["MINI_APP_ALLOWED_ORIGIN"]?.Trim() ?? string.Empty,
            GitHubDeployToken = configuration["GITHUB_DEPLOY_TOKEN"]?.Trim() ?? string.Empty,
            GitHubRepo = configuration["GITHUB_REPO"]?.Trim() ?? string.Empty
        };
    }

    private static bool IsRunningInDocker()
    {
        return File.Exists("/.dockerenv") || 
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
    }
}
