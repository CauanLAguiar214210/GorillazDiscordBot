using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Entity;

public class GuildPrefixSettings : IGuildSettings
{
    public ulong GuildId { get; set; }
    public string? Prefix { get; set; }
}
