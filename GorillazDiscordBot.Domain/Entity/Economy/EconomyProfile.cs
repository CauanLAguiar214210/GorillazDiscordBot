namespace GorillazDiscordBot.Domain.Entity.Economy;

public class EconomyProfile
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Money { get; set; }
    public int Bank { get; set; }
    public DateTime? LastDailyClaim { get; set; }
    public int Savings { get; set; }
    public int SavingsStreak { get; set; }
    public DateTime? SavingsLastInterestDate { get; set; }
    public DateTime? LastWorkTime { get; set; }
    public DateTime? LastRobTime { get; set; }
    public DateTime? RobCaughtUntil { get; set; }
}