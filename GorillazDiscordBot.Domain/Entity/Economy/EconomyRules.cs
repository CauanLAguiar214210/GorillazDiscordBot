using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Domain.Entity.Economy;

public enum JobCategory
{
    SubEmprego,
    Emprego,
    Veiculo
}

public enum JobPayMode
{
    Legacy,
    Clicker
}

public sealed record Job(
    string Key,
    string Name,
    string Emoji,
    int Hours,
    int HourlyPay,
    JobCategory Category,
    SchoolingLevel MinSchooling = SchoolingLevel.Nenhuma,
    bool RequiresDiploma = false,
    LicenseLevel? RequiredLicense = null,
    VehicleType? RequiredVehicleType = null,
    JobPayMode PayMode = JobPayMode.Legacy,
    int CategoryBonusPercent = 0,
    int TypeBonusPercent = 0,
    LicenseDomain? VehicleDomain = null)
{
    public ulong TotalPay => (ulong)(Hours * HourlyPay);
    public bool IsEmprego => Category == JobCategory.Emprego;
    public bool IsVeiculo => Category == JobCategory.Veiculo;
    public bool IsSubEmprego => Category == JobCategory.SubEmprego;
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
        new Job("professor", "Professor", "👨‍🏫", 1, 120, JobCategory.Emprego, SchoolingLevel.EnsinoSuperior, true),

        new Job("piloto-aviao", "Piloto de Avião", "✈️", 5, 210, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.PilotoPrivado, RequiredVehicleType: VehicleType.Aviao,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 5,
            VehicleDomain: LicenseDomain.Aerea),
        new Job("piloto-comercial", "Piloto Comercial", "🛫", 6, 260, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.PilotoComercial, RequiredVehicleType: VehicleType.Aviao,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 5,
            VehicleDomain: LicenseDomain.Aerea),
        new Job("piloto-linha-aerea", "Piloto de Linha Aérea", "🛩️", 7, 320, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.PilotoLinhaAerea, RequiredVehicleType: VehicleType.Jato,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 10,
            VehicleDomain: LicenseDomain.Aerea),
        new Job("motorista-app", "Motorista de App", "🚕", 3, 90, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.B, RequiredVehicleType: VehicleType.Carro,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 0,
            VehicleDomain: LicenseDomain.Terrestre),

        new Job("condutor-lancha", "Condutor de Lancha", "🚤", 4, 150, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.Arrais, RequiredVehicleType: VehicleType.Lancha,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 5,
            VehicleDomain: LicenseDomain.Maritima),
        new Job("comandante-iate", "Comandante de Iate", "🛥️", 5, 230, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.Mestre, RequiredVehicleType: VehicleType.Iate,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 5,
            VehicleDomain: LicenseDomain.Maritima),
        new Job("capitao-navio", "Capitão de Navio", "🚢", 7, 310, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.Capitao, RequiredVehicleType: VehicleType.Navio,
            PayMode: JobPayMode.Legacy, CategoryBonusPercent: 20, TypeBonusPercent: 10,
            VehicleDomain: LicenseDomain.Maritima),
        new Job("manobrista", "Manobrista", "🚗", 2, 10, JobCategory.Veiculo,
            RequiredLicense: LicenseLevel.B,
            PayMode: JobPayMode.Clicker, CategoryBonusPercent: 0, TypeBonusPercent: 0,
            VehicleDomain: LicenseDomain.Terrestre),
    };

    public static IReadOnlyList<Job> SubEmpregos { get; } =
        All.Where(j => j.Category == JobCategory.SubEmprego).ToList();

    public static IReadOnlyList<Job> Empregos { get; } =
        All.Where(j => j.Category == JobCategory.Emprego).ToList();

    public static IReadOnlyList<Job> Veiculos { get; } =
        All.Where(j => j.Category == JobCategory.Veiculo).ToList();

    public static IReadOnlyList<Job> VeiculosByDomain(LicenseDomain domain)
        => All.Where(j => j.Category == JobCategory.Veiculo && j.VehicleDomain == domain).ToList();

    public static IReadOnlyList<LicenseDomain> VehicleDomains { get; } =
        All.Where(j => j.Category == JobCategory.Veiculo && j.VehicleDomain is not null)
           .Select(j => j.VehicleDomain!.Value)
           .Distinct()
           .OrderBy(d => d)
           .ToList();

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

    public const double BankDailyInterestMin = 0.001;
    public const double BankDailyInterestMax = 0.003;

    public const int AssetQuotasPerShare = 100;
    public const double AssetBaseVolatility = 0.20;

    public const double RobSuccessChance = 0.40;
    public const double RobVictimShare = 0.20;
    public const ulong RobMaxSteal = 1000;
    public static readonly TimeSpan RobCooldown = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan RobCaughtLockout = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan FaculdadeCooldown = TimeSpan.FromMinutes(5);

    public static TimeSpan WorkCooldown(Job job)
        => job.RequiresDiploma || JobGameCatalog.ForJobKey(job.Key) is not null
            ? FaculdadeCooldown
            : TimeSpan.FromHours(job.Hours);

    public static ulong GetDailyReward(Random rng)
        => (ulong)rng.Next((int)DailyMin, (int)DailyMax + 1);

    public static (double Min, double Max) GetInterestRateRange(ulong streak)
    {
        var bonus = Math.Min(streak, InterestStreakMaxBonus) * InterestStreakBonus;
        return (DailyInterestMin + bonus, DailyInterestMax + bonus);
    }

    public static (double Min, double Max) GetBankInterestRange()
        => (BankDailyInterestMin, BankDailyInterestMax);

    public static double GetDailyInterestRate(Random rng, ulong streak)
    {
        var baseRate = DailyInterestMin + (rng.NextDouble() * (DailyInterestMax - DailyInterestMin));
        var bonus = Math.Min(streak, InterestStreakMaxBonus) * InterestStreakBonus;
        return baseRate + bonus;
    }

    public static ulong ComputeInterestAmount(ulong savings, double rate)
        => (ulong)Math.Floor(savings * rate);

    public static ulong NextSavingsStreak(ulong currentStreak, DateTime? lastDepositDate, DateTime now)
    {
        if (lastDepositDate is null) return 1;

        var last = lastDepositDate.Value.Date;
        var today = now.Date;

        if (last == today) return currentStreak;
        if (last == today.AddDays(-1)) return currentStreak + 1;
        return 1;
    }

    public static double GetVolatilityFactor(string assetKey, DateTime? day = null)
    {
        var d = (day ?? DateTime.UtcNow).Date;

        var hash = StableHashUInt(assetKey);
        var ticks = (ulong)d.Ticks;
        hash ^= (uint)(ticks & 0xFFFFFFFF);
        hash ^= (uint)((ticks >> 32) & 0xFFFFFFFF);

        var t = (hash % 2_000_001) / 1_000_000.0 - 1.0;
        return 1.0 + t * AssetBaseVolatility;
    }

    public static double GetPriceVariation(string assetKey, DateTime day)
    {
        var today = GetVolatilityFactor(assetKey, day);
        var yesterday = GetVolatilityFactor(assetKey, day.AddDays(-1));
        return (today / yesterday - 1.0) * 100.0;
    }

    public static ulong ComputeCotaPrice(string assetKey, ulong basePrice, DateTime? day = null)
        => (ulong)Math.Round(basePrice * GetVolatilityFactor(assetKey, day) / AssetQuotasPerShare);

    public static ulong ComputeQuotaPrice(string assetKey, ulong basePrice, int cotas, DateTime? day = null)
        => ComputeCotaPrice(assetKey, basePrice, day) * (ulong)cotas;

    public static ulong ComputeQuotaIncome(ulong dailyIncome, int cotas)
        => dailyIncome * (ulong)Math.Max(0, cotas) / (ulong)AssetQuotasPerShare;

    private static uint StableHashUInt(string input)
    {
        uint hash = 2166136261;
        foreach (var c in input)
        {
            hash ^= c;
            hash *= 16777619;
        }

        return hash;
    }

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