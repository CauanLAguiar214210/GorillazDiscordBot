namespace GorillazDiscordBot.Domain.Entity.Ranking;

public class HallOfFame
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Phrase { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}