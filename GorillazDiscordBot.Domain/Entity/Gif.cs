namespace GorillazDiscordBot.Entity;

public class Gif
{
    public string Id { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Categoria { get; set; } = "geral";
    public ulong AddedBy { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
