using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SnakeFrogCalendarBot.Worker.Api;

public sealed class MiniAppTokenService
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, (long UserId, DateTimeOffset ExpiresAt)> _tokens = new();

    public string Generate(long userId)
    {
        PurgeExpired();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _tokens[token] = (userId, DateTimeOffset.UtcNow.Add(TokenTtl));
        return token;
    }

    public long? Consume(string token)
    {
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
