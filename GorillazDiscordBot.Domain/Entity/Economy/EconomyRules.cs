using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Domain.Entity.Economy;

public enum JobCategory
{
    SubEmprego,
    Emprego
}

public sealed record Job(
    string Key,
    string Name,
    string Emoji,
    int Hours,
    int HourlyPay,
    JobCategory Category,
    SchoolingLevel MinSchooling,
    bool RequiresDiploma)
{
    public ulong TotalPay => (ulong)(Hours * HourlyPay);
    public bool IsEmprego => Category == JobCategory.Emprego;
}

public static class EconomyJobs
{
    public static readonly IReadOnlyList<Job> All = new[]
    {
        new Job("entregador", "Entregador", "🛵", 2, 50, JobCategory.SubEmprego, SchoolingLevel.Nenhuma, false),
        new Job("faxineiro", "Faxineiro", "🧹", 3, 60, JobCategory.SubEmprego, SchoolingLevel.Nenhuma, false),
        new Job("porteiro", "Porteiro", "🚪", 4, 70, JobCategory.SubEmprego, SchoolingLevel.Nenhuma, false),
        new Job("cozinheiro", "Cozinheiro", "👨‍🍳", 5, 80, JobCategory.SubEmprego, SchoolingLevel.Nenhuma, false),
        new Job("programador", "Programador", "💻", 6, 100, JobCategory.Emprego, SchoolingLevel.EnsinoMedio, true),
        new Job("engenheiro", "Engenheiro", "🛠️", 8, 90, JobCategory.Emprego, SchoolingLevel.EnsinoSuperior, true),
    };

    public static IReadOnlyList<Job> SubEmpregos { get; } =
        All.Where(j => j.Category == JobCategory.SubEmprego).ToList();

    public static IReadOnlyList<Job> Empregos { get; } =
        All.Where(j => j.Category == JobCategory.Emprego).ToList();

    public static Job? Find(string alias)
        => All.FirstOrDefault(j => j.Key.Equals(alias, StringComparison.OrdinalIgnoreCase)
                                   || j.Name.Equals(alias, StringComparison.OrdinalIgnoreCase));

    public static Job? FindByKey(string key)
        => All.FirstOrDefault(j => j.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}

public static class EconomyRules
{
    public const ulong DailyMin = 100;
    public const ulong DailyMax = 500;

    public const double DailyInterestMin = 0.005;
    public const double DailyInterestMax = 0.03;
    public const double InterestStreakBonus = 0.005;
    public const ulong InterestStreakMaxBonus = 6;

    public const double RobSuccessChance = 0.40;
    public const double RobVictimShare = 0.20;
    public const ulong RobMaxSteal = 1000;
    public static readonly TimeSpan RobCooldown = TimeSpan.FromMinutes(0);
    public static readonly TimeSpan RobCaughtLockout = TimeSpan.FromMinutes(3);

    public static ulong GetDailyReward(Random rng)
        => (ulong)rng.Next((int)DailyMin, (int)DailyMax + 1);

    public static double GetDailyInterestRate(Random rng, ulong streak)
    {
        var baseRate = DailyInterestMin + (rng.NextDouble() * (DailyInterestMax - DailyInterestMin));
        var bonus = Math.Min(streak, InterestStreakMaxBonus) * InterestStreakBonus;
        return baseRate + bonus;
    }

    public static ulong ComputeInterestAmount(ulong savings, double rate)
        => (ulong)Math.Floor(savings * rate);

    public static bool ShouldRobSucceed(Random rng)
        => rng.NextDouble() < RobSuccessChance;

    public static ulong ComputeRobAmount(ulong victimMoney, Random rng)
    {
        var amount = (ulong)Math.Floor(victimMoney * RobVictimShare);
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