namespace GorillazDiscordBot.Entity;

public class GuildMember
{
    public string Id { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<UserWarning> Warnings { get; set; } = new();
    public DateTime? MuteUntil { get; set; }
    public bool IsBanned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}