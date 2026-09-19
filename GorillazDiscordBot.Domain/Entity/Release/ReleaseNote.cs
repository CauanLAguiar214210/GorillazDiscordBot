namespace GorillazDiscordBot.Domain.Entity.Release;

public enum ReleaseFeatureType
{
    New = 0,
    Improvement = 1,
    Fix = 2
}

public class ReleaseFeature
{
    public ReleaseFeatureType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ReleaseNote
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime PublishedAt { get; set; }
    public List<ReleaseFeature> Features { get; set; } = new();
    public DateTime? AnnouncedAt { get; set; }
}
