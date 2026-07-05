using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SnakeFrogCalendarBot.Worker.Config;

namespace SnakeFrogCalendarBot.Worker.Api;

public sealed class MiniAppTokenService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(10);

    private readonly string _botToken;
    private readonly ConcurrentDictionary<string, (long UserId, DateTimeOffset ExpiresAt)> _tokens = new();
    private readonly ConcurrentDictionary<string, long> _persistentTokenLookup = new();

    public MiniAppTokenService(AppOptions options)
    {
        _botToken = options.TelegramBotToken;
    }

    public string Generate(long userId)
    {
        PurgeExpired();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tokens[token] = (userId, DateTimeOffset.UtcNow.Add(TokenTtl));
        return token;
    }

    /// <summary>
    /// Returns a deterministic reusable token for the user derived from the bot token.
    /// Survives service restarts — pre-warm at startup for all allowed user IDs.
    /// </summary>
    public string GetOrCreatePersistent(long userId)
    {
        var key = Encoding.UTF8.GetBytes(_botToken);
        var data = Encoding.UTF8.GetBytes(userId.ToString());
        var hash = HMACSHA256.HashData(key, data);
        var token = Convert.ToHexString(hash).ToLowerInvariant();
        _persistentTokenLookup[token] = userId;
        return token;
    }

    public long? Consume(string token)
    {
        if (_persistentTokenLookup.TryGetValue(token, out var persistentUserId))
            return persistentUserId;

        if (!_tokens.TryRemove(token, out var entry))
            return null;

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
            return null;

        return entry.UserId;
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _tokens.Keys)
        {
            if (_tokens.TryGetValue(key, out var v) && v.ExpiresAt < now)
                _tokens.TryRemove(key, out _);
        }
    }
}
