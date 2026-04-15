using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SnakeFrogCalendarBot.Worker.Api;

public sealed class MiniAppTokenService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (long UserId, DateTimeOffset ExpiresAt)> _tokens = new();
    private readonly ConcurrentDictionary<long, string> _persistentTokens = new();
    private readonly ConcurrentDictionary<string, long> _persistentTokenLookup = new();

    public string Generate(long userId)
    {
        PurgeExpired();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tokens[token] = (userId, DateTimeOffset.UtcNow.Add(TokenTtl));
        return token;
    }

    /// <summary>
    /// Returns a reusable token for the user, creating one if it doesn't exist.
    /// Used for reply keyboard WebApp URLs where the URL is static.
    /// </summary>
    public string GetOrCreatePersistent(long userId)
    {
        if (_persistentTokens.TryGetValue(userId, out var existing))
            return existing;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        token = _persistentTokens.GetOrAdd(userId, token);
        _persistentTokenLookup[token] = userId;
        return token;
    }

    public long? Consume(string token)
    {
        // Check persistent tokens first (reusable, not consumed)
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
