using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        // Parse raw pairs WITHOUT URL-decoding: Telegram computes the HMAC over
        // the raw URL-encoded values (e.g. user=%7B%22id%22%3A1%7D, not user={"id":1}).
        string? hash = null;
        string? rawAuthDate = null;
        string? rawUserJson = null;
        var dataPairs = new List<string>();

        foreach (var segment in initData.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq < 0) continue;
            var key = segment[..eq];
            var rawValue = segment[(eq + 1)..];

            if (key == "hash")
            {
                hash = rawValue;
            }
            else
            {
                dataPairs.Add(segment);
                if (key == "auth_date") rawAuthDate = rawValue;
                if (key == "user") rawUserJson = Uri.UnescapeDataString(rawValue);
            }
        }

        if (string.IsNullOrEmpty(hash))
            return null;

        // Build data-check-string: raw pairs sorted alphabetically, joined with \n
        dataPairs.Sort(StringComparer.Ordinal);
        var dataCheckString = string.Join('\n', dataPairs);

        // secret_key = HMAC-SHA256(key="WebAppData", data=bot_token)
        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(options.TelegramBotToken));

        // calculated = HMAC-SHA256(key=secret_key, data=data_check_string)
        var calculated = HMACSHA256.HashData(
            secretKey,
            Encoding.UTF8.GetBytes(dataCheckString));

        var calculatedHex = Convert.ToHexString(calculated).ToLowerInvariant();
        if (!string.Equals(calculatedHex, hash, StringComparison.OrdinalIgnoreCase))
            return null;

        // Check auth_date freshness
        if (!long.TryParse(rawAuthDate, out var authDate))
            return null;

        var authDateUtc = DateTimeOffset.FromUnixTimeSeconds(authDate);
        if (DateTimeOffset.UtcNow - authDateUtc > MaxAge)
            return null;

        // Extract user id from URL-decoded user JSON
        if (string.IsNullOrEmpty(rawUserJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawUserJson);
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
