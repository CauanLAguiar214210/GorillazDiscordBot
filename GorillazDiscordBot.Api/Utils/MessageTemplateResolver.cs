namespace GorillazDiscordBot.Utils;

public static class MessageTemplateResolver
{
    public static string Resolve(string template, string? userMention = null, string? serverName = null, int? memberCount = null)
    {
        var result = template;

        if (userMention != null)
            result = result.Replace("{user}", userMention);
        if (serverName != null)
            result = result.Replace("{server}", serverName);
        if (memberCount.HasValue)
            result = result.Replace("{count}", memberCount.Value.ToString());

        return result;
    }
}
