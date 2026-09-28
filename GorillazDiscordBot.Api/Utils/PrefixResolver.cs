namespace GorillazDiscordBot.Utils;

public static class PrefixResolver
{
    public static string Resolve(string? prefixo, string defaultPrefix)
        => !string.IsNullOrWhiteSpace(prefixo)
            ? prefixo
            : defaultPrefix;
}