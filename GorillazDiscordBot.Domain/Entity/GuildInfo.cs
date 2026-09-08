namespace GorillazDiscordBot.Entity;

public class GuildInfo
{
    public string? Name { get; set; }
    public string? IconUrl { get; set; }
    public ulong? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public int MemberCount { get; set; }
    public int BoostCount { get; set; }
    public int BoostLevel { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? PreferredLocale { get; set; }
}