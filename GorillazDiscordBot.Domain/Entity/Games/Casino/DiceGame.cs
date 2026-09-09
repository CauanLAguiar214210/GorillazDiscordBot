namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum DiceBetType
{
    High,
    Low,
    Seven,
    Doubles
}

public sealed class DiceGame
{
    private readonly Func<int> _roll;

    public DiceBetType BetType { get; }

    public (int First, int Second)? Result { get; private set; }

    public bool HasRolled => Result.HasValue;

    public DiceGame(DiceBetType betType, Func<int>? roll = null)
    {
        BetType = betType;
        _roll = roll ?? (() => Random.Shared.Next(1, 7));
    }

    public (int First, int Second) Roll()
    {
        if (HasRolled)
            throw new InvalidOperationException("Estes dados já foram rolados.");

        var result = (_roll(), _roll());
        Result = result;
        return result;
    }

    public int Total => Result is { } r ? r.First + r.Second : 0;

    public bool IsDoubles => Result is { } r && r.First == r.Second;

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!HasRolled || !IsWin(BetType, Total, IsDoubles))
            return 0;

        return bet * (ulong)Multiplier(BetType);
    }

    public static bool IsWin(DiceBetType type, int total, bool isDoubles)
    {
        return type switch
        {
            DiceBetType.High => total is >= 8 and <= 12,
            DiceBetType.Low => total is >= 2 and <= 6,
            DiceBetType.Seven => total == 7,
            DiceBetType.Doubles => isDoubles,
            _ => false
        };
    }

    public static int Multiplier(DiceBetType type) => type switch
    {
        DiceBetType.High => CasinoRules.DiceHighPayout,
        DiceBetType.Low => CasinoRules.DiceLowPayout,
        DiceBetType.Seven => CasinoRules.DiceSevenPayout,
        DiceBetType.Doubles => CasinoRules.DiceDoublesPayout,
        _ => 0
    };
}