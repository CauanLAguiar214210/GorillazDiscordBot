namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class CrimeRules
{
    public static readonly TimeSpan FurtoCooldown = TimeSpan.Zero;
    public static readonly TimeSpan CrimeCooldown = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan PrisonLockout = TimeSpan.FromMinutes(1);

    public const double BaseFurtoSuccessChance = 0.70;
    public const ulong MinFurtoReward = 50;
    public const ulong MaxFurtoReward = 350;

    public const double BaseRobSuccessChance = 0.40;
    public const double RobVictimShare = 0.20;
    public const ulong BaseRobMaxSteal = 1000000;

    public const double FineRate = 0.15;
    public const ulong MinFine = 100;
    public const ulong BaseBailAmount = 500;

    public static ulong ComputeFurtoReward(Random rng, int bonusPercent = 0)
    {
        var baseReward = (ulong)rng.Next((int)MinFurtoReward, (int)MaxFurtoReward + 1);
        if (bonusPercent > 0)
            baseReward += baseReward * (ulong)bonusPercent / 100;

        return Math.Max(1, baseReward);
    }

    public static bool ShouldFurtoSucceed(Random rng, int bonusChancePercent = 0)
    {
        var chance = BaseFurtoSuccessChance + (bonusChancePercent / 100.0);
        chance = Math.Clamp(chance, 0.10, 0.95);
        return rng.NextDouble() < chance;
    }

    public static bool ShouldRobSucceed(Random rng, int attackerBonusPercent = 0, int victimDefensePercent = 0)
    {
        var chance = BaseRobSuccessChance + ((attackerBonusPercent - victimDefensePercent) / 100.0);
        chance = Math.Clamp(chance, 0.10, 0.85);
        return rng.NextDouble() < chance;
    }

    public static bool DidVictimCounterAttack(Random rng, int victimCounterPercent)
    {
        if (victimCounterPercent <= 0) return false;
        var chance = Math.Clamp(victimCounterPercent / 100.0, 0.05, 0.60);
        return rng.NextDouble() < chance;
    }

    public static ulong ComputeRobAmount(ulong victimMoney, ulong maxStealCap, int petBonusPercent = 0)
    {
        var amount = (ulong)Math.Floor(victimMoney * RobVictimShare);
        var cap = Math.Max(BaseRobMaxSteal, maxStealCap);
        amount = Math.Min(amount, cap);

        if (petBonusPercent > 0)
            amount += amount * (ulong)petBonusPercent / 100;

        return Math.Max(1, amount);
    }

    public static ulong ComputeFine(ulong thiefMoney)
    {
        var fine = (ulong)Math.Floor(thiefMoney * FineRate);
        return Math.Max(MinFine, fine);
    }

    public static ulong ComputeBail(ulong netWorth)
    {
        var extra = (ulong)Math.Min(25_000, Math.Floor(netWorth * 0.005));
        return BaseBailAmount + extra;
    }
}
