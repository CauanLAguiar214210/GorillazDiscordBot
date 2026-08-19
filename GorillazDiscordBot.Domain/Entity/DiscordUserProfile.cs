namespace GorillazDiscordBot.Entity;

public class DiscordUserProfile
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Money { get; set; }
    public int Bank { get; set; }
    public DateTime? LastDailyClaim { get; set; }
}
