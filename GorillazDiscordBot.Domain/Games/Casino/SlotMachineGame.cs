namespace GorillazDiscordBot.Domain.Games;

public enum SlotSymbol
{
    Cherry,
    Lemon,
    Bell,
    Star,
    Seven,
    Diamond
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

    public static int CalculateReturn(int bet, IReadOnlyList<SlotSymbol> reels)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (reels.Count < 3)
            return 0;

        var a = reels[0];
        var b = reels[1];
        var c = reels[2];

        if (a == b && b == c)
            return bet * Multiplier(a);

        if (a == b || a == c || b == c)
        {
            var pair = a == b ? a : c;
            return bet * PairMultiplier(pair);
        }

        return 0;
    }

    public static int Multiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 2,
        SlotSymbol.Lemon => 3,
        SlotSymbol.Bell => 5,
        SlotSymbol.Star => 10,
        SlotSymbol.Seven => 20,
        SlotSymbol.Diamond => 50,
        _ => 0
    };

    public static int PairMultiplier(SlotSymbol symbol) => symbol switch
    {
        SlotSymbol.Cherry => 1,
        SlotSymbol.Lemon => 2,
        SlotSymbol.Bell => 3,
        SlotSymbol.Star => 5,
        SlotSymbol.Seven => 8,
        SlotSymbol.Diamond => 15,
        _ => 0
    };

    private static SlotSymbol DefaultRoll()
    {
        var roll = Random.Shared.NextDouble();

        return roll switch
        {
            < 0.30 => SlotSymbol.Cherry,
            < 0.50 => SlotSymbol.Lemon,
            < 0.66 => SlotSymbol.Bell,
            < 0.80 => SlotSymbol.Star,
            < 0.92 => SlotSymbol.Seven,
            _ => SlotSymbol.Diamond
        };
    }
}
