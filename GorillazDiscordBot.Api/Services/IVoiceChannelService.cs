using Discord.WebSocket;

namespace GorillazDiscordBot.Services;

public interface IVoiceChannelService
{
    Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after);
}
