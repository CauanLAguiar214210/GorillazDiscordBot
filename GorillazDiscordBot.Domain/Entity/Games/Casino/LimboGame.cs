namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public sealed class LimboGame
{
    private readonly Func<double> _draw;

    public double Target { get; }

    public double? Result { get; private set; }

    public bool HasRolled => Result.HasValue;

    public bool IsWin => HasRolled && Result!.Value >= Target;

    public LimboGame(double target, Func<double>? draw = null)
    {
        if (target < 1.0)
            throw new ArgumentOutOfRangeException(nameof(target), "O alvo deve ser de pelo menos 1.0x.");

        Target = target;
        _draw = draw ?? (() => Random.Shared.NextDouble());
    }

    public double Roll()
    {
        if (HasRolled)
            throw new InvalidOperationException("O número já foi revelado.");

        var uniform = 1.0 - _draw();
        var result = 1.0 / Math.Max(uniform, double.Epsilon);
        if (result > 10_000)
            result = 10_000;

        Result = result;
        return result;
    }

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!HasRolled || !IsWin)
            return 0;

        return (ulong)Math.Floor(bet * Target * CasinoRules.LimboHouseEdge);
    }
}