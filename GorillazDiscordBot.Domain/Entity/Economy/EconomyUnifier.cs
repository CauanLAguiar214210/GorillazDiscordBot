namespace GorillazDiscordBot.Domain.Entity.Economy;

public sealed record UnifyResult(ulong MergedMoney, ulong MergedBank, ulong MergedSavings);

public static class EconomyUnifier
{
    public static UnifyResult Unify(EconomyProfile source, EconomyProfile target)
    {
        target.Money = CheckedAdd(target.Money, source.Money);
        target.Bank = CheckedAdd(target.Bank, source.Bank);
        target.Savings = CheckedAdd(target.Savings, source.Savings);
        target.SavingsStreak = Math.Max(target.SavingsStreak, source.SavingsStreak);
        target.LastDailyClaim = Later(target.LastDailyClaim, source.LastDailyClaim);
        target.LastWorkTime = Later(target.LastWorkTime, source.LastWorkTime);
        target.LastRobTime = Later(target.LastRobTime, source.LastRobTime);
        target.RobCaughtUntil = Later(target.RobCaughtUntil, source.RobCaughtUntil);
        target.SavingsLastInterestDate = Later(target.SavingsLastInterestDate, source.SavingsLastInterestDate);

        target.DailyBoostPending = target.DailyBoostPending || source.DailyBoostPending;
        target.WorkBoostPending = target.WorkBoostPending || source.WorkBoostPending;
        target.DailyBoostExpiresAt = Later(target.DailyBoostExpiresAt, source.DailyBoostExpiresAt);
        target.WorkBoostExpiresAt = Later(target.WorkBoostExpiresAt, source.WorkBoostExpiresAt);
        target.RobShieldUntil = Later(target.RobShieldUntil, source.RobShieldUntil);

        return new UnifyResult(source.Money, source.Bank, source.Savings);
    }

    public static ulong CheckedAdd(ulong a, ulong b)
    {
        var sum = a + b;
        return sum < a ? ulong.MaxValue : sum;
    }

    private static DateTime? Later(DateTime? a, DateTime? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        return a.Value >= b.Value ? a : b;
    }
}