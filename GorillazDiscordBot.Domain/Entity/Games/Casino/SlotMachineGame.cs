namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum SlotSymbol
{
    Cherry,
    Lemon,
    Bell,
    Clover,
    Star,
    Seven,
    Skull,
    Diamond,
    Crown
}

public sealed class SlotMachineGame
{
    private readonly Func<SlotSymbol> _roll;

    public IReadOnlyList<SlotSymbol> Reels { get; } = new List<SlotSymbol>(3);

    public bool HasSpun => Reels.Count > 0;

    public SlotMachineGame(Func<SlotSymbol>? roll = null)
    {
        _roll = roll ?? DefaultRoll;
    }

    public IReadOnlyList<SlotSymbol> Spin()
    {
        if (HasSpun)
            throw new InvalidOperationException("Esta máquina já foi girada.");

        var reels = (List<SlotSymbol>)Reels;
        for (var i = 0; i < 3; i++)
            reels.Add(_roll());

        return Reels;
    }

    public static ulong CalculateReturn(ulong bet, IReadOnlyList<SlotSymbol> reels)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (reels.Count < 3)
            return 0;

        var a = reels[0];
        var b = reels[1];
        var c = reels[2];

        if (a == b && b == c)
            return bet * (ulong)Multiplier(a);

        if (a == b || a == c || b == c)
        {
            var pair = a == b ? a : c;
            return bet * (ulong)PairMultiplier(pair);
        }

        return 0;
    }

    public static int Multiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 2,
        SlotSymbol.Lemon => 3,
        SlotSymbol.Bell => 4,
        SlotSymbol.Clover => 2,
        SlotSymbol.Star => 6,
        SlotSymbol.Seven => 15,
        SlotSymbol.Skull => 10,
        SlotSymbol.Diamond => 40,
        SlotSymbol.Crown => 50,
        _ => 0
    };

    public static int PairMultiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 1,
        SlotSymbol.Lemon => 2,
        SlotSymbol.Bell => 3,
        SlotSymbol.Clover => 1,
        SlotSymbol.Star => 3,
        SlotSymbol.Seven => 7,
        SlotSymbol.Skull => 4,
        SlotSymbol.Diamond => 12,
        SlotSymbol.Crown => 15,
        _ => 0
    };

    private static SlotSymbol DefaultRoll()
    {
        var roll = Random.Shared.NextDouble();

        return roll switch
        {
            < 0.23 => SlotSymbol.Cherry,
            < 0.40 => SlotSymbol.Lemon,
            < 0.53 => SlotSymbol.Bell,
            < 0.69 => SlotSymbol.Clover,
            < 0.82 => SlotSymbol.Star,
            < 0.92 => SlotSymbol.Seven,
            < 0.96 => SlotSymbol.Skull,
            < 0.99 => SlotSymbol.Crown,
            _ => SlotSymbol.Diamond
        };
    }
}
