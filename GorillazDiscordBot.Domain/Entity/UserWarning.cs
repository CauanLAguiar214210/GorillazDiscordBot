namespace GorillazDiscordBot.Entity;

public class UserWarning
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Reason { get; set; } = string.Empty;
    public ulong AddedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}