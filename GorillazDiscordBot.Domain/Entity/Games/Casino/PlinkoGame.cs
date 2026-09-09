namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public sealed class PlinkoGame
{
    private readonly Func<int> _bin;

    public int? ResultBin { get; private set; }

    public bool HasDropped => ResultBin.HasValue;

    public PlinkoGame(Func<int>? bin = null)
    {
        _bin = bin ?? DefaultBin;
    }

    public int Drop()
    {
        if (HasDropped)
            throw new InvalidOperationException("A bolinha já foi lançada.");

        var bin = _bin();
        if (bin < 0 || bin >= MultiplierCount)
            throw new InvalidOperationException("Faixa de queda inválida.");

        ResultBin = bin;
        return bin;
    }

    public static int MultiplierCount => 9;

    public static double Multiplier(int bin)
    {
        if (bin < 0 || bin >= MultiplierCount)
            throw new ArgumentOutOfRangeException(nameof(bin), "Faixa inválida.");

        return Multipliers[bin].Multiplier;
    }

    public static int Weight(int bin)
    {
        if (bin < 0 || bin >= MultiplierCount)
            throw new ArgumentOutOfRangeException(nameof(bin), "Faixa inválida.");

        return Multipliers[bin].Weight;
    }

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!HasDropped)
            return 0;

        return (ulong)Math.Floor(bet * Multipliers[ResultBin!.Value].Multiplier);
    }

    public static int DefaultBin()
    {
        var heads = 0;
        for (var i = 0; i < 8; i++)
        {
            if (Random.Shared.Next(2) == 0)
                heads++;
        }
        return heads;
    }

    public static readonly Quota[] Multipliers =
    {
        new(3.0, 1),
        new(2.0, 8),
        new(1.4, 28),
        new(0.9, 56),
        new(0.5, 70),
        new(0.9, 56),
        new(1.4, 28),
        new(2.0, 8),
        new(3.0, 1)
    };

    public readonly record struct Quota(double Multiplier, int Weight);
}