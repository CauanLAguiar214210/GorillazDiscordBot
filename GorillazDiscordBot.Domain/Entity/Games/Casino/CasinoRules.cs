namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public static class CasinoRules
{
    public const int MinBet = 10;
    public const int MaxBet = 1_000_000;

    public const int RouletteNumberCount = 37;

    public const int RouletteStraightPayout = 36;

    public const double RouletteEvenMoneyPayout = 2.0;

    public static bool IsValidBet(int amount)
        => amount >= MinBet && amount <= MaxBet;
}
