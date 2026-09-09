namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public sealed class AviaoGame
{
    private readonly double _crashPoint;
    private double _currentMultiplier;

    public ulong Bet { get; }

    public double CurrentMultiplier => Math.Round(_currentMultiplier, 2);

    public double CrashMultiplier { get; private set; }

    public bool HasCrashed { get; private set; }

    public bool HasCashedOut { get; private set; }

    public bool IsFinished => HasCrashed || HasCashedOut;

    public AviaoGame(ulong bet, Func<double>? crashPoint = null)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        Bet = bet;
        _crashPoint = Math.Clamp(
            (crashPoint ?? DefaultCrashPoint)(),
            CasinoRules.AviaoMinCrash,
            CasinoRules.AviaoMaxCrash);
        _currentMultiplier = CasinoRules.AviaoStartMultiplier;
    }

    public bool Fly()
    {
        if (IsFinished)
            throw new InvalidOperationException("Este aviãozinho já encerrou o voo.");

        _currentMultiplier += CasinoRules.AviaoStepMultiplier;

        if (_currentMultiplier >= _crashPoint)
        {
            _currentMultiplier = _crashPoint;
            CrashMultiplier = _crashPoint;
            HasCrashed = true;
        }

        return HasCrashed;
    }

    public ulong CashOut()
    {
        if (IsFinished)
            throw new InvalidOperationException("Este aviãozinho já encerrou o voo.");

        HasCashedOut = true;
        return (ulong)Math.Floor(Bet * _currentMultiplier);
    }

    private static double DefaultCrashPoint()
        => CasinoRules.AviaoMinCrash
            + (Random.Shared.NextDouble() * (CasinoRules.AviaoMaxCrash - CasinoRules.AviaoMinCrash));
}