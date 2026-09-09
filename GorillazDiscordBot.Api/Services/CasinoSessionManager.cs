using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

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

    public DiceGame? Dice { get; }

    public CoinFlipGame? Coin { get; }

    public AviaoGame? Aviao { get; }

    public VideoPokerGame? VideoPoker { get; }

    public MinesGame? Mines { get; }

    public LimboGame? Limbo { get; }

    public RpsGame? Rps { get; }

    public RaceGame? Race { get; }

    public PlinkoGame? Plinko { get; }

    public WheelGame? Wheel { get; }

    public HighLowGame? HighLow { get; }

    public BaccaratGame? Baccarat { get; }

    public ulong Bet { get; }

    private CasinoSession(
        ulong bet, RouletteGame? roulette, SlotMachineGame? slots, DiceGame? dice,
        CoinFlipGame? coin, AviaoGame? aviao, VideoPokerGame? videoPoker,
        MinesGame? mines, LimboGame? limbo, RpsGame? rps, RaceGame? race, PlinkoGame? plinko,
        WheelGame? wheel, HighLowGame? highLow, BaccaratGame? baccarat)
    {
        Bet = bet;
        Roulette = roulette;
        Slots = slots;
        Dice = dice;
        Coin = coin;
        Aviao = aviao;
        VideoPoker = videoPoker;
        Mines = mines;
        Limbo = limbo;
        Rps = rps;
        Race = race;
        Plinko = plinko;
        Wheel = wheel;
        HighLow = highLow;
        Baccarat = baccarat;
    }

    public static CasinoSession ForRoulette(ulong bet, RouletteGame roulette)
        => new(bet, roulette, null, null, null, null, null, null, null, null, null, null, null, null, null);

    public static CasinoSession ForSlots(ulong bet, SlotMachineGame slots)
        => new(bet, null, slots, null, null, null, null, null, null, null, null, null, null, null, null);

    public static CasinoSession ForDice(ulong bet, DiceGame dice)
        => new(bet, null, null, dice, null, null, null, null, null, null, null, null, null, null, null);

    public static CasinoSession ForCoin(ulong bet, CoinFlipGame coin)
        => new(bet, null, null, null, coin, null, null, null, null, null, null, null, null, null, null);

    public static CasinoSession ForAviao(ulong bet, AviaoGame aviao)
        => new(bet, null, null, null, null, aviao, null, null, null, null, null, null, null, null, null);

    public static CasinoSession ForVideoPoker(ulong bet, VideoPokerGame videoPoker)
        => new(bet, null, null, null, null, null, videoPoker, null, null, null, null, null, null, null, null);

    public static CasinoSession ForMines(ulong bet, MinesGame mines)
        => new(bet, null, null, null, null, null, null, mines, null, null, null, null, null, null, null);

    public static CasinoSession ForLimbo(ulong bet, LimboGame limbo)
        => new(bet, null, null, null, null, null, null, null, limbo, null, null, null, null, null, null);

    public static CasinoSession ForRps(ulong bet, RpsGame rps)
        => new(bet, null, null, null, null, null, null, null, null, rps, null, null, null, null, null);

    public static CasinoSession ForRace(ulong bet, RaceGame race)
        => new(bet, null, null, null, null, null, null, null, null, null, race, null, null, null, null);

    public static CasinoSession ForPlinko(ulong bet, PlinkoGame plinko)
        => new(bet, null, null, null, null, null, null, null, null, null, null, plinko, null, null, null);

    public static CasinoSession ForWheel(ulong bet, WheelGame wheel)
        => new(bet, null, null, null, null, null, null, null, null, null, null, null, wheel, null, null);

    public static CasinoSession ForHighLow(ulong bet, HighLowGame highLow)
        => new(bet, null, null, null, null, null, null, null, null, null, null, null, null, highLow, null);

    public static CasinoSession ForBaccarat(ulong bet, BaccaratGame baccarat)
        => new(bet, null, null, null, null, null, null, null, null, null, null, null, null, null, baccarat);
}
