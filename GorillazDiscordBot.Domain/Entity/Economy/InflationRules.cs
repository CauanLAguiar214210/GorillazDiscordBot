namespace GorillazDiscordBot.Domain.Entity.Economy;

/// <summary>
/// Sistema de inflação desativado. Index retorna 1.0 e Inflate retorna o valor base sem modificação.
/// </summary>
public static class InflationRules
{
    public static double Index(ulong moneySupply = 0) => 1.0;

    public static ulong Inflate(ulong baseValue, ulong moneySupply = 0) => baseValue;
}
