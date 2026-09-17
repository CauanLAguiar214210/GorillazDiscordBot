namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class InflationRules
{
    public const ulong SupplyDivisor = 250_000;

    public static double Index(ulong moneySupply)
        => 1.0 + (double)moneySupply / SupplyDivisor;

    public static ulong Inflate(ulong baseValue, ulong moneySupply)
        => (ulong)Math.Round(baseValue * Index(moneySupply));
}