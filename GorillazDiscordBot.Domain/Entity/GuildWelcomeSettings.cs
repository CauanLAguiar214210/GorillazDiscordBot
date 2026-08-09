using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Entity;

public class GuildWelcomeSettings : IGuildSettings
{
    public ulong GuildId { get; set; }
    public ulong? WelcomeChannelId { get; set; }
    public ulong? GoodbyeChannelId { get; set; }
    public string WelcomeMessage { get; set; } = "Bem-vindo(a) {user} ao **{server}**! 🎉\nVocê é o membro nº **{count}**!";
    public string GoodbyeMessage { get; set; } = "Tchau {user}! 😢\nVolte sempre ao **{server}**!";
    public bool WelcomeEnabled { get; set; }
    public bool GoodbyeEnabled { get; set; }
}
