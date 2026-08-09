namespace GorillazDiscordBot.Entity;

public class GuildInteraction
{
    public string Id { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public ulong AddedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
