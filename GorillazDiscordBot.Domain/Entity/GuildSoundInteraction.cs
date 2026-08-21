namespace GorillazDiscordBot.Entity;

public class GuildSoundInteraction
{
    public string Id { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string GridFsId { get; set; } = string.Empty;
    public long FileLengthBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public ulong AddedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
