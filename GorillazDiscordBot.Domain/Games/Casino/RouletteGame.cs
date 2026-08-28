namespace GorillazDiscordBot.Domain.Games;

public enum RouletteBetType
{
    Number,
    Color,
    Parity,
    Half
}

public enum RouletteColor
{
    Red,
    Black,
    Zero
}

public sealed record RouletteBet(int Amount, RouletteBetType Type, int Target);

public sealed class RouletteGame
{
    private readonly Func<int> _roll;

    public IReadOnlyList<RouletteBet> Bets { get; } = new List<RouletteBet>();

    public int? ResultNumber { get; private set; }

    public bool HasSpun => ResultNumber.HasValue;

    public RouletteGame(Func<int>? roll = null)
    {
        _roll = roll ?? (() => Random.Shared.Next(CasinoRules.RouletteNumberCount));
    }

    public RouletteBet AddBet(int amount, RouletteBetType type, int target)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "A aposta deve ser positiva.");

        ValidateTarget(type, target);

        var bet = new RouletteBet(amount, type, target);
        ((List<RouletteBet>)Bets).Add(bet);
        return bet;
    }

    public int Spin()
    {
        if (HasSpun)
            throw new InvalidOperationException("Esta roletas já foi girada.");

        ResultNumber = _roll();
        return ResultNumber.Value;
    }

    public bool IsWin(RouletteBet bet)
    {
        if (!HasSpun)
            return false;

        var number = ResultNumber!.Value;
        return bet.Type switch
        {
            RouletteBetType.Number => number == bet.Target,
            RouletteBetType.Color => number != 0 && ColorOf(number) == (RouletteColor)bet.Target,
            RouletteBetType.Parity => number != 0 && number % 2 == bet.Target,
            RouletteBetType.Half => bet.Target == 0 ? number >= 1 && number <= 18 : number >= 19,
            _ => false
        };
    }

    public int CalculateReturn(RouletteBet bet)
    {
        if (!HasSpun || !IsWin(bet))
            return 0;

        return bet.Type == RouletteBetType.Number
            ? bet.Amount * CasinoRules.RouletteStraightPayout
            : (int)Math.Floor(bet.Amount * CasinoRules.RouletteEvenMoneyPayout);
    }

    public int CalculateTotalReturn()
    {
        if (!HasSpun)
            return 0;

        var total = 0;
        foreach (var bet in Bets)
            total += CalculateReturn(bet);
        return total;
    }

    public int TotalBet => Bets.Sum(b => b.Amount);

    public static RouletteColor ColorOf(int number)
    {
        if (number == 0)
            return RouletteColor.Zero;

        int[] reds =
        {
            1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
        };
        return reds.Contains(number) ? RouletteColor.Red : RouletteColor.Black;
    }

    private static void ValidateTarget(RouletteBetType type, int target)
    {
        switch (type)
        {
            case RouletteBetType.Number:
                if (target < 0 || target > CasinoRules.RouletteNumberCount - 1)
                    throw new ArgumentOutOfRangeException(nameof(target), "Número deve estar entre 0 e 36.");
                break;
            case RouletteBetType.Color:
                if ((RouletteColor)target is not (RouletteColor.Red or RouletteColor.Black))
                    throw new ArgumentOutOfRangeException(nameof(target), "Cor deve ser Vermelho (0) ou Preto (1).");
                break;
            case RouletteBetType.Parity:
                if (target is not (0 or 1))
                    throw new ArgumentOutOfRangeException(nameof(target), "Paridade deve ser Par (0) ou Ímpar (1).");
                break;
            case RouletteBetType.Half:
                if (target is not (0 or 1))
                    throw new ArgumentOutOfRangeException(nameof(target), "Metade deve ser Baixa (0) ou Alta (1).");
                break;
        }
    }
}
