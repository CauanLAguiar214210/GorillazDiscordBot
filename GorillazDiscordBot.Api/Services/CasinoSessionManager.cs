using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Games;

namespace GorillazDiscordBot.Services;

public sealed class CasinoSessionManager
{
    private sealed record Entry(CasinoSession Session, DateTimeOffset LastActivity);

    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<ulong, Entry> _sessions = new();

    public TimeSpan IdleTimeout { get; }

    public CasinoSessionManager()
        : this(() => DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5))
    {
    }

    public CasinoSessionManager(Func<DateTimeOffset> utcNow, TimeSpan idleTimeout)
    {
        _utcNow = utcNow;
        IdleTimeout = idleTimeout;
    }

    public CasinoSession? GetActive(ulong userId)
    {
        return _sessions.TryGetValue(userId, out var entry) && !IsExpired(entry)
            ? entry.Session
            : null;
    }

    public CasinoSession? TakeExpired(ulong userId)
    {
        if (!_sessions.TryGetValue(userId, out var entry) || !IsExpired(entry))
            return null;

        return _sessions.TryRemove(new KeyValuePair<ulong, Entry>(userId, entry))
            ? entry.Session
            : null;
    }

    public void Add(ulong userId, CasinoSession session)
    {
        _sessions[userId] = new Entry(session, _utcNow());
    }

    public void Touch(ulong userId)
    {
        if (_sessions.TryGetValue(userId, out var entry))
            _sessions[userId] = entry with { LastActivity = _utcNow() };
    }

    public CasinoSession? Remove(ulong userId)
    {
        return _sessions.TryRemove(userId, out var entry) ? entry.Session : null;
    }

    private bool IsExpired(Entry entry) => _utcNow() - entry.LastActivity >= IdleTimeout;
}

public sealed class CasinoSession
{
    public RouletteGame? Roulette { get; }

    public SlotMachineGame? Slots { get; }

    public int Bet { get; }

    private CasinoSession(int bet, RouletteGame? roulette, SlotMachineGame? slots)
    {
        Bet = bet;
        Roulette = roulette;
        Slots = slots;
    }

    public static CasinoSession ForRoulette(int bet, RouletteGame roulette)
        => new(bet, roulette, null);

    public static CasinoSession ForSlots(int bet, SlotMachineGame slots)
        => new(bet, null, slots);
}
