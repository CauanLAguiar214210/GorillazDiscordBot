namespace GorillazDiscordBot.Entity;

public class DiscordUserProfile
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Points { get; set; }
    public DateTime? LastDailyClaim { get; set; }
}
