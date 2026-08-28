namespace GorillazDiscordBot.Economy;

public sealed record Job(string Key, string Name, string Emoji, int Hours, int HourlyPay)
{
    public int TotalPay => Hours * HourlyPay;
}

public static class EconomyJobs
{
    public static readonly IReadOnlyList<Job> All = new[]
    {
        new Job("entregador", "Entregador", "🛵", 2, 50),
        new Job("faxineiro", "Faxineiro", "🧹", 3, 60),
        new Job("porteiro", "Porteiro", "🚪", 4, 70),
        new Job("cozinheiro", "Cozinheiro", "👨‍🍳", 5, 80),
        new Job("programador", "Programador", "💻", 6, 100),
        new Job("engenheiro", "Engenheiro", "🛠️", 8, 90),
    };

    public static Job? Find(string alias)
        => All.FirstOrDefault(j => j.Key.Equals(alias, StringComparison.OrdinalIgnoreCase)
                                   || j.Name.Equals(alias, StringComparison.OrdinalIgnoreCase));
}

public static class EconomyRules
{
    public const int DailyMin = 100;
    public const int DailyMax = 500;

    public const double DailyInterestMin = 0.005;
    public const double DailyInterestMax = 0.03;
    public const double InterestStreakBonus = 0.005;
    public const int InterestStreakMaxBonus = 6;

    public const double RobSuccessChance = 0.40;
    public const double RobVictimShare = 0.20;
    public const int RobMaxSteal = 1000;
    public static readonly TimeSpan RobCooldown = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RobCaughtLockout = TimeSpan.FromHours(3);

    public static int GetDailyReward(Random rng)
        => rng.Next(DailyMin, DailyMax + 1);

    public static double GetDailyInterestRate(Random rng, int streak)
    {
        var baseRate = DailyInterestMin + (rng.NextDouble() * (DailyInterestMax - DailyInterestMin));
        var bonus = Math.Min(Math.Max(streak, 0), InterestStreakMaxBonus) * InterestStreakBonus;
        return baseRate + bonus;
    }

    public static int ComputeInterestAmount(int savings, double rate)
        => (int)Math.Floor(savings * rate);

    public static bool ShouldRobSucceed(Random rng)
        => rng.NextDouble() < RobSuccessChance;

    public static int ComputeRobAmount(int victimMoney, Random rng)
    {
        var amount = (int)Math.Floor(victimMoney * RobVictimShare);
        amount = Math.Min(amount, RobMaxSteal);
        return Math.Max(1, amount);
    }

    public static TimeSpan? GetRemainingCooldown(DateTime? lastAttempt, DateTime now, TimeSpan cooldown)
    {
        if (lastAttempt == null) return null;
        var elapsed = now - lastAttempt.Value;
        if (elapsed >= cooldown) return null;
        return cooldown - elapsed;
    }
}