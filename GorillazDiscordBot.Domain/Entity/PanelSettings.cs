namespace GorillazDiscordBot.Entity;

public class PanelSettings
{
    public bool Enabled { get; set; }

    public Dictionary<string, ulong> Channels { get; set; } = new();
}
