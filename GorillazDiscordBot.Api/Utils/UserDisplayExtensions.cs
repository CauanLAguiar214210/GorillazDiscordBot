using Discord;

namespace GorillazDiscordBot.Utils;

public static class UserDisplayExtensions
{
    public static string GetDisplayName(this IUser user)
        => user is IGuildUser guildUser
            ? guildUser.Nickname ?? guildUser.DisplayName ?? guildUser.Username
            : user.GlobalName ?? user.Username;
}
