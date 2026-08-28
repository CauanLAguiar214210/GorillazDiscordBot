namespace GorillazDiscordBot.Entity;

public class DiscordUserProfile
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}