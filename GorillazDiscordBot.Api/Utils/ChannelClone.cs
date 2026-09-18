using Discord;

namespace GorillazDiscordBot.Utils;

public static class ChannelClone
{
    public static async Task<ITextChannel> CloneAsync(ITextChannel source)
    {
        var guild = source.Guild;

        var clone = await guild.CreateTextChannelAsync(source.Name, props =>
        {
            props.CategoryId = source.CategoryId;
            props.Topic = source.Topic;
            props.SlowModeInterval = source.SlowModeInterval;
            props.IsNsfw = source.IsNsfw;
            props.Position = source.Position;
        });

        foreach (var overwrite in source.PermissionOverwrites)
        {
            try
            {
                if (overwrite.TargetType == PermissionTarget.Role)
                    await clone.AddPermissionOverwriteAsync(guild.GetRole(overwrite.TargetId), overwrite.Permissions);
                else
                    await clone.AddPermissionOverwriteAsync(await guild.GetUserAsync(overwrite.TargetId), overwrite.Permissions);
            }
            catch
            {
            }
        }

        return clone;
    }
}