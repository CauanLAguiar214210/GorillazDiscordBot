using Discord;
using Discord.WebSocket;

namespace GorillazDiscordBot.Events;

public interface IBotEventSink
{
    void Subscribe(DiscordSocketClient client);
    void Unsubscribe(DiscordSocketClient client);
}

public abstract class BotEventSink : IBotEventSink
{
    public void Subscribe(DiscordSocketClient client)
    {
        client.MessageReceived += OnMessageReceivedAsync;
        client.MessageDeleted += OnMessageDeletedAsync;
        client.MessageUpdated += OnMessageUpdatedAsync;
        client.UserBanned += OnUserBannedAsync;
        client.UserUnbanned += OnUserUnbannedAsync;
        client.UserJoined += OnUserJoinedAsync;
        client.UserLeft += OnUserLeftAsync;
        client.GuildAvailable += OnGuildAvailableAsync;
        client.GuildUpdated += OnGuildUpdatedAsync;
        client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;
    }

    public void Unsubscribe(DiscordSocketClient client)
    {
        client.MessageReceived -= OnMessageReceivedAsync;
        client.MessageDeleted -= OnMessageDeletedAsync;
        client.MessageUpdated -= OnMessageUpdatedAsync;
        client.UserBanned -= OnUserBannedAsync;
        client.UserUnbanned -= OnUserUnbannedAsync;
        client.UserJoined -= OnUserJoinedAsync;
        client.UserLeft -= OnUserLeftAsync;
        client.GuildAvailable -= OnGuildAvailableAsync;
        client.GuildUpdated -= OnGuildUpdatedAsync;
        client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;
    }

    public virtual Task OnMessageReceivedAsync(SocketMessage message) => Task.CompletedTask;

    public virtual Task OnMessageDeletedAsync(
        Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel) => Task.CompletedTask;

    public virtual Task OnMessageUpdatedAsync(
        Cacheable<IMessage, ulong> before,
        SocketMessage after,
        ISocketMessageChannel channel) => Task.CompletedTask;

    public virtual Task OnUserBannedAsync(SocketUser user, SocketGuild guild) => Task.CompletedTask;

    public virtual Task OnUserUnbannedAsync(SocketUser user, SocketGuild guild) => Task.CompletedTask;

    public virtual Task OnUserJoinedAsync(SocketGuildUser user) => Task.CompletedTask;

    public virtual Task OnUserLeftAsync(SocketGuild guild, SocketUser user) => Task.CompletedTask;

    public virtual Task OnGuildAvailableAsync(SocketGuild guild) => Task.CompletedTask;

    public virtual Task OnGuildUpdatedAsync(SocketGuild before, SocketGuild after) => Task.CompletedTask;

    public virtual Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after) => Task.CompletedTask;
}