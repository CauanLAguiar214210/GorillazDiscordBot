namespace GorillazDiscordBot.Domain.Entity.Economy;

public enum ManobristaEvent
{
    Nenhum,
    Gorjeta,
    Vip,
    Riscado
}

public static class ManobristaRules
{
    public const int Vagas = 20;
    public const ulong BasePerCar = 10;
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan ComboWindow = TimeSpan.FromSeconds(3);
    public const double ComboStep = 0.20;
    public const double ComboMaxBonus = 1.00;

    public const double GorjetaChance = 0.18;
    public const ulong GorjetaReward = 20;
    public const double VipChance = 0.03;
    public const double VipMultiplier = 3.0;
    public const double RiscadoChance = 0.06;

    public static double ComboMultiplier(int combo)
        => 1.0 + Math.Min(Math.Max(0, combo - 1) * ComboStep, ComboMaxBonus);
}