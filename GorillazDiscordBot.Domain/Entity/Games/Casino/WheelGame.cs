namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public sealed class WheelGame
{
    private readonly Func<int> _spin;

    public ulong Bet { get; }

    public int? ResultIndex { get; private set; }

    public bool HasSpun => ResultIndex.HasValue;

    public double ResultMultiplier => HasSpun ? Multiplier(ResultIndex!.Value) : 0;

    public WheelGame(ulong bet, Func<int>? spin = null)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        Bet = bet;
        _spin = spin ?? (() => Random.Shared.Next(Slots.Length));
    }

    public int Spin()
    {
        if (HasSpun)
            throw new InvalidOperationException("Esta roda já foi girada.");

        var index = _spin();
        if (index < 0 || index >= Slots.Length)
            throw new InvalidOperationException("Fatia inválida.");

        ResultIndex = index;
        return index;
    }

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!HasSpun)
            return 0;

        return (ulong)Math.Floor(bet * Multiplier(ResultIndex!.Value) * CasinoRules.WheelHouseEdge);
    }

    public static double Multiplier(int index)
    {
        if (index < 0 || index >= Slots.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Fatia inválida.");

        return Slots[index];
    }

    public static double ExpectedReturn => Slots.Sum() / Slots.Length * CasinoRules.WheelHouseEdge;

    public static readonly double[] Slots =
    {
        0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5,
        1.0,
        5.0
    };
}