using Discord;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Services;

public static class DmNotifier
{
    public static async Task<bool> TryNotifyAsync(
        IUser user,
        string title,
        string description,
        Color color)
    {
        try
        {
            var dm = await user.CreateDMChannelAsync();
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color)
                .WithStandardFooter("Ação de moderação aplicada em um servidor")
                .Build();

            await dm.SendMessageAsync(embed: embed);
            return true;
        }
        catch
        {
            return false;
        }
    }
}