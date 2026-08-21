using Discord;

namespace GorillazDiscordBot.Utils;

public static class EmbedExtensions
{
    public static EmbedBuilder WithStandardFooter(this EmbedBuilder embed, string text)
        => embed.WithFooter(text)
                .WithTimestamp(DateTimeOffset.UtcNow);

    public static EmbedBuilder WithGoldTheme(this EmbedBuilder embed)
        => embed.WithColor(Color.Gold);

    public static EmbedBuilder WithBlurpleTheme(this EmbedBuilder embed)
        => embed.WithColor(new Color(0x5865F2));

    public static EmbedBuilder WithStatus(this EmbedBuilder embed, string label, bool isEnabled)
        => embed.AddField(label, isEnabled ? BotConstants.Enabled : BotConstants.Disabled, true);

    public static EmbedBuilder WithChannelField(this EmbedBuilder embed, string label, ulong? channelId, IGuild guild)
    {
        var channelName = channelId.HasValue
            ? $"<#{channelId.Value}>"
            : BotConstants.NotSet;
        return embed.AddField(label, channelName, true);
    }
}
