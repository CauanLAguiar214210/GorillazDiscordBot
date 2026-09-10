namespace GorillazDiscordBot.Domain.Entity.Ranking;

public class RankingTier
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public ulong MinNetWorth { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}