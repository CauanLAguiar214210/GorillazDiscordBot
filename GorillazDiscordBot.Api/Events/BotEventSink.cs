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
        client.MessagesBulkDeleted += OnMessagesBulkDeletedAsync;
        client.UserBanned += OnUserBannedAsync;
        client.UserUnbanned += OnUserUnbannedAsync;
        client.UserJoined += OnUserJoinedAsync;
        client.UserLeft += OnUserLeftAsync;
        client.GuildMemberUpdated += OnGuildMemberUpdatedAsync;
        client.GuildAvailable += OnGuildAvailableAsync;
        client.GuildUpdated += OnGuildUpdatedAsync;
        client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;
        client.ChannelCreated += OnChannelCreatedAsync;
        client.ChannelDestroyed += OnChannelDestroyedAsync;
        client.ChannelUpdated += OnChannelUpdatedAsync;
    }

    public void Unsubscribe(DiscordSocketClient client)
    {
        client.MessageReceived -= OnMessageReceivedAsync;
        client.MessageDeleted -= OnMessageDeletedAsync;
        client.MessageUpdated -= OnMessageUpdatedAsync;
        client.MessagesBulkDeleted -= OnMessagesBulkDeletedAsync;
        client.UserBanned -= OnUserBannedAsync;
        client.UserUnbanned -= OnUserUnbannedAsync;
        client.UserJoined -= OnUserJoinedAsync;
        client.UserLeft -= OnUserLeftAsync;
        client.GuildMemberUpdated -= OnGuildMemberUpdatedAsync;
        client.GuildAvailable -= OnGuildAvailableAsync;
        client.GuildUpdated -= OnGuildUpdatedAsync;
        client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;
        client.ChannelCreated -= OnChannelCreatedAsync;
        client.ChannelDestroyed -= OnChannelDestroyedAsync;
        client.ChannelUpdated -= OnChannelUpdatedAsync;
    }

    public virtual Task OnMessageReceivedAsync(SocketMessage message) => Task.CompletedTask;

    public virtual Task OnMessageDeletedAsync(
        Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel) => Task.CompletedTask;

    public virtual Task OnMessageUpdatedAsync(
        Cacheable<IMessage, ulong> before,
        SocketMessage after,
        ISocketMessageChannel channel) => Task.CompletedTask;

    public virtual Task OnMessagesBulkDeletedAsync(
        IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel) => Task.CompletedTask;

    public virtual Task OnUserBannedAsync(SocketUser user, SocketGuild guild) => Task.CompletedTask;

    public virtual Task OnUserUnbannedAsync(SocketUser user, SocketGuild guild) => Task.CompletedTask;

    public virtual Task OnUserJoinedAsync(SocketGuildUser user) => Task.CompletedTask;

    public virtual Task OnUserLeftAsync(SocketGuild guild, SocketUser user) => Task.CompletedTask;

    public virtual Task OnGuildMemberUpdatedAsync(
        Cacheable<SocketGuildUser, ulong> before,
        SocketGuildUser after) => Task.CompletedTask;

    public virtual Task OnGuildAvailableAsync(SocketGuild guild) => Task.CompletedTask;

    public virtual Task OnGuildUpdatedAsync(SocketGuild before, SocketGuild after) => Task.CompletedTask;

    public virtual Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after) => Task.CompletedTask;

    public virtual Task OnChannelCreatedAsync(SocketChannel channel) => Task.CompletedTask;

    public virtual Task OnChannelDestroyedAsync(SocketChannel channel) => Task.CompletedTask;

    public virtual Task OnChannelUpdatedAsync(SocketChannel before, SocketChannel after) => Task.CompletedTask;
}