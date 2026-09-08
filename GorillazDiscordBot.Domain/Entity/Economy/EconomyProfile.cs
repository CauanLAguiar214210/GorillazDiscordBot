namespace GorillazDiscordBot.Domain.Entity.Economy;

public class EconomyProfile
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public ulong Money { get; set; }
    public ulong Bank { get; set; }
    public DateTime? LastDailyClaim { get; set; }
    public ulong Savings { get; set; }
    public ulong SavingsStreak { get; set; }
    public DateTime? SavingsLastInterestDate { get; set; }
    public DateTime? LastWorkTime { get; set; }
    public DateTime? LastRobTime { get; set; }
    public DateTime? RobCaughtUntil { get; set; }

    public bool DailyBoostPending { get; set; }
    public bool WorkBoostPending { get; set; }
    public DateTime? RobShieldUntil { get; set; }
    public DateTime? DailyBoostExpiresAt { get; set; }
    public DateTime? WorkBoostExpiresAt { get; set; }

    public ulong NetWorth => AddSafe(Money, AddSafe(Bank, Savings));

    private static ulong AddSafe(ulong a, ulong b)
    {
        var sum = a + b;
        return sum < a ? ulong.MaxValue : sum;
    }
}