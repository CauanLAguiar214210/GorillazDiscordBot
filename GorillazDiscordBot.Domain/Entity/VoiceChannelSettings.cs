namespace GorillazDiscordBot.Entity;

public class VoiceChannelSettings
{
    public const string DefaultNameTemplate = "Resenhando com {name}...";
    public const int DefaultUserLimit = 10;

    public ulong CreatorChannelId { get; set; }
    public bool Enabled { get; set; }
    public string? NameTemplate { get; set; }
    public int? UserLimit { get; set; }
    public ulong? CategoryId { get; set; }
}