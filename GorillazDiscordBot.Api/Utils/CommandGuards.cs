using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace GorillazDiscordBot.Utils;

public static class CommandGuards
{
    public static bool HasManageGuildPermission(SocketCommandContext context)
        => context.Guild != null &&
           context.User is IGuildUser guildUser &&
           (guildUser.GuildPermissions.ManageGuild || guildUser.GuildPermissions.Administrator);

    public static async Task<bool> GuardPermissionAsync(SocketCommandContext context)
    {
        if (!HasManageGuildPermission(context))
        {
            await context.Channel.SendMessageAsync(BotConstants.PermissionDenied);
            return false;
        }
        return true;
    }

    public static async Task<bool> GuardGuildOnlyAsync(SocketCommandContext context)
    {
        if (context.Guild == null)
        {
            await context.Channel.SendMessageAsync(BotConstants.GuildOnly);
            return false;
        }
        return true;
    }
}
