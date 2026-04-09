using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Http;
using SnakeFrogCalendarBot.Worker.Config;

namespace SnakeFrogCalendarBot.Worker.Api;

public static class TelegramInitDataValidator
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    /// <summary>
    /// Validates the Telegram initData from the Authorization header.
    /// Returns the Telegram user ID on success, or null on failure.
    /// </summary>
    public static long? Validate(HttpRequest request, AppOptions options)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("tma ", StringComparison.OrdinalIgnoreCase))
            return null;

        var initData = authHeader["tma ".Length..].Trim();
        if (string.IsNullOrEmpty(initData))
            return null;

        var parsed = HttpUtility.ParseQueryString(initData);

        var hash = parsed["hash"];
        if (string.IsNullOrEmpty(hash))
            return null;

        // Build data-check-string: all fields except hash, sorted, joined with \n
        var pairs = new List<string>();
        foreach (string? key in parsed)
        {
            if (key is null || key == "hash") continue;
            pairs.Add($"{key}={parsed[key]}");
        }
        pairs.Sort(StringComparer.Ordinal);
        var dataCheckString = string.Join('\n', pairs);

        // secret_key = HMAC-SHA256("WebAppData", bot_token)
        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(options.TelegramBotToken));

        // calculated = HMAC-SHA256(secret_key, data_check_string)
        var calculated = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes(dataCheckString));

        var calculatedHex = Convert.ToHexString(calculated).ToLowerInvariant();
        if (!string.Equals(calculatedHex, hash, StringComparison.OrdinalIgnoreCase))
            return null;

        // Check auth_date freshness
        if (!long.TryParse(parsed["auth_date"], out var authDate))
            return null;

        var authDateUtc = DateTimeOffset.FromUnixTimeSeconds(authDate);
        if (DateTimeOffset.UtcNow - authDateUtc > MaxAge)
            return null;

        // Extract user id from user JSON field
        var userJson = parsed["user"];
        if (string.IsNullOrEmpty(userJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(userJson);
            if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out var userId))
                return userId;
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
