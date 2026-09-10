namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public static class CasinoRules
{
    public const ulong MinBet = 10;
    public const ulong MaxBet = 1_000_000;

    public const int RouletteNumberCount = 37;

    public const int RouletteStraightPayout = 36;

    public const double RouletteEvenMoneyPayout = 2.0;

    public const int DiceHighPayout = 2;
    public const int DiceLowPayout = 2;
    public const int DiceSevenPayout = 5;
    public const int DiceDoublesPayout = 6;

    public const int CoinFlipPayout = 2;

    public const double AviaoStartMultiplier = 1.0;
    public const double AviaoStepMultiplier = 0.1;
    public const double AviaoMinCrash = 1.0;
    public const double AviaoMaxCrash = 5.0;

    public const int MinesDefaultCount = 4;
    public const int MinesGridSize = 16;
    public const double MinesHouseEdge = 0.95;

    public const double LimboHouseEdge = 0.97;

    public const int RpsPayout = 2;

    public const int RaceRunners = 6;

    public const double WheelHouseEdge = 0.95;

    public const double HighLowMultiplierStep = 2.0;

    public const double BaccaratBankerPayout = 1.95;

    public const int BaccaratTiePayout = 9;

    public static bool IsValidBet(ulong amount)
        => amount >= MinBet && amount <= MaxBet;
}
