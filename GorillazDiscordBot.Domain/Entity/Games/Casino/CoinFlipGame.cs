namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum CoinSide
{
    Cara,
    Coroa
}

public sealed class CoinFlipGame
{
    private readonly Func<bool> _flip;

    public CoinSide Side { get; }

    public CoinSide? Result { get; private set; }

    public bool HasFlipped => Result.HasValue;

    public CoinFlipGame(CoinSide side, Func<bool>? flip = null)
    {
        Side = side;
        _flip = flip ?? (() => Random.Shared.Next(2) == 0);
    }

    public CoinSide Flip()
    {
        if (HasFlipped)
            throw new InvalidOperationException("Esta moeda já foi lançada.");

        var result = _flip() ? CoinSide.Cara : CoinSide.Coroa;
        Result = result;
        return result;
    }

    public ulong CalculateReturn(ulong bet) => CalculateReturn(Side, Result ?? throw new InvalidOperationException("A moeda ainda não foi lançada."), bet);

    public static bool IsWin(CoinSide chosen, CoinSide result)
        => chosen == result;

    public static ulong CalculateReturn(CoinSide chosen, CoinSide result, ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!IsWin(chosen, result))
            return 0;

        return bet * (ulong)CasinoRules.CoinFlipPayout;
    }
}