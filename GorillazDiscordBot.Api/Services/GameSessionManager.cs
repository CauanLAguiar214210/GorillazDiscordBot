using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Services;

public sealed class GameSessionManager
{
    private sealed record Entry(BlackjackGame Game, DateTimeOffset LastActivity);

    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<ulong, Entry> _sessions = new();

    public TimeSpan IdleTimeout { get; }

    public GameSessionManager()
        : this(() => DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5))
    {
    }

    public GameSessionManager(Func<DateTimeOffset> utcNow, TimeSpan idleTimeout)
    {
        _utcNow = utcNow;
        IdleTimeout = idleTimeout;
    }

    public BlackjackGame? GetActive(ulong userId)
    {
        return _sessions.TryGetValue(userId, out var entry) && !IsExpired(entry)
            ? entry.Game
            : null;
    }

    public BlackjackGame? TakeExpired(ulong userId)
    {
        if (!_sessions.TryGetValue(userId, out var entry) || !IsExpired(entry))
            return null;

        return _sessions.TryRemove(new KeyValuePair<ulong, Entry>(userId, entry))
            ? entry.Game
            : null;
    }

    public void Add(ulong userId, BlackjackGame game)
    {
        _sessions[userId] = new Entry(game, _utcNow());
    }

    public void Touch(ulong userId)
    {
        if (_sessions.TryGetValue(userId, out var entry))
            _sessions[userId] = entry with { LastActivity = _utcNow() };
    }

    public BlackjackGame? Remove(ulong userId)
    {
        return _sessions.TryRemove(userId, out var entry) ? entry.Game : null;
    }

    private bool IsExpired(Entry entry) => _utcNow() - entry.LastActivity >= IdleTimeout;
}
