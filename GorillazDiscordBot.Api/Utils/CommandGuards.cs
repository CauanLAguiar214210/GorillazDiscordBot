using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

namespace GorillazDiscordBot.Utils;

public static class CommandGuards
{
    public static bool HasManageGuildPermission(IGuildUser guildUser)
        => guildUser.GuildPermissions.ManageGuild || guildUser.GuildPermissions.Administrator;

    public static bool HasManageGuildPermission(SocketCommandContext context)
        => context.Guild != null &&
           context.User is IGuildUser guildUser &&
           HasManageGuildPermission(guildUser);

    public static bool HasManageGuildPermission(SocketInteractionContext context)
        => context.Guild != null &&
           context.User is IGuildUser guildUser &&
           HasManageGuildPermission(guildUser);

    public static async Task<bool> GuardPermissionAsync(SocketCommandContext context)
    {
        if (!HasManageGuildPermission(context))
        {
            await context.Channel.SendMessageAsync(BotConstants.PermissionDenied);
            return false;
        }
        return true;
    }

    public static async Task<bool> GuardAdminInteractionAsync(SocketInteractionContext context)
    {
        if (HasManageGuildPermission(context))
            return true;

        await context.Interaction.RespondAsync(BotConstants.PermissionDenied, ephemeral: true);
        return false;
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

    public static bool HasPermission(SocketCommandContext context, GuildPermission permission)
        => context.Guild != null && context.User is IGuildUser user && user.GuildPermissions.Has(permission);

    public static bool HasPermission(SocketInteractionContext context, GuildPermission permission)
        => context.Guild != null && context.User is IGuildUser user && user.GuildPermissions.Has(permission);

    public static bool BotHasPermission(SocketCommandContext context, GuildPermission permission)
        => context.Guild != null && context.Guild.CurrentUser.GuildPermissions.Has(permission);

    public static bool BotHasPermission(SocketInteractionContext context, GuildPermission permission)
        => context.Guild != null && context.Guild.CurrentUser.GuildPermissions.Has(permission);

    public static async Task<bool> GuardPermissionAsync(SocketCommandContext context, GuildPermission permission)
    {
        if (HasPermission(context, permission))
            return true;

        await context.Channel.SendMessageAsync($"❌ Você precisa da permissão **{PermissionName(permission)}** para usar este comando.");
        return false;
    }

    public static async Task<bool> GuardBotPermissionAsync(SocketCommandContext context, GuildPermission permission)
    {
        if (BotHasPermission(context, permission))
            return true;

        await context.Channel.SendMessageAsync($"⚠️ O bot precisa da permissão **{PermissionName(permission)}** para executar esta ação.");
        return false;
    }

    public static async Task<bool> GuardPermissionAsync(SocketInteractionContext context, GuildPermission permission)
    {
        if (HasPermission(context, permission))
            return true;

        await context.Interaction.RespondAsync($"❌ Você precisa da permissão **{PermissionName(permission)}** para usar este comando.", ephemeral: true);
        return false;
    }

    public static async Task<bool> GuardBotPermissionAsync(SocketInteractionContext context, GuildPermission permission)
    {
        if (BotHasPermission(context, permission))
            return true;

        await context.Interaction.RespondAsync($"⚠️ O bot precisa da permissão **{PermissionName(permission)}** para executar esta ação.", ephemeral: true);
        return false;
    }

    public static string PermissionName(GuildPermission permission)
        => permission switch
        {
            GuildPermission.ManageMessages => "Gerenciar Mensagens",
            GuildPermission.ManageChannels => "Gerenciar Canais",
            GuildPermission.KickMembers => "Expulsar Membros",
            GuildPermission.BanMembers => "Banir Membros",
            GuildPermission.ModerateMembers => "Moderar Membros",
            _ => permission.ToString()
        };
}
