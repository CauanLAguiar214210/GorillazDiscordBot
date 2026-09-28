using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Events;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class GuildLogService : BotEventSink
{
    private static readonly TimeSpan KickMarkerTtl = TimeSpan.FromMinutes(10);

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly ILogger<GuildLogService> _logger;
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), (DateTimeOffset At, string? Reason)> _kickedMarkers = new();

    public GuildLogService(ISettingsRepository<Guild> guildRepository, ILogger<GuildLogService> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    public void MarkKicked(ulong guildId, ulong userId, string? reason)
        => _kickedMarkers[(guildId, userId)] = (DateTimeOffset.UtcNow, reason);

    public Task RecordAsync(SocketGuild guild, Func<GuildLogSettings, bool> gate, Embed embed)
        => TrySendModerationAsync(guild, gate, embed);

    public override async Task OnMessageDeletedAsync(
        Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        try
        {
            if (!channel.HasValue || channel.Value is not SocketGuildChannel guildChannel)
                return;

            var settings = await _guildRepository.GetAsync(guildChannel.Guild.Id);
            var log = settings.Logs;
            if (log == null || !log.Enabled || !log.LogMessageDeletes || !log.ChannelId.HasValue)
                return;

            var logChannel = guildChannel.Guild.GetTextChannel(log.ChannelId.Value);
            if (logChannel == null)
                return;

            string? author = null;
            string? content = null;
            if (message.HasValue)
            {
                author = message.Value.Author?.GetDisplayName();
                content = message.Value.Content;
            }

            var embed = GuildLogEmbeds.MessageDeleted(
                string.IsNullOrWhiteSpace(author) ? BotConstants.Unknown : author,
                string.IsNullOrWhiteSpace(content) ? null : content,
                guildChannel.Name,
                message.Id);

            await logChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar exclusao de mensagem");
        }
    }

    public override async Task OnMessageUpdatedAsync(
        Cacheable<IMessage, ulong> before,
        SocketMessage after,
        ISocketMessageChannel socketChannel)
    {
        try
        {
            if (after.Channel is not SocketGuildChannel guildChannel)
                return;

            var settings = await _guildRepository.GetAsync(guildChannel.Guild.Id);
            var log = settings.Logs;
            if (log == null || !log.Enabled || !log.LogMessageEdits || !log.ChannelId.HasValue)
                return;

            var beforeContent = before.HasValue ? before.Value.Content : null;
            if (!before.HasValue || string.IsNullOrWhiteSpace(beforeContent))
                return;

            var logChannel = guildChannel.Guild.GetTextChannel(log.ChannelId.Value);
            if (logChannel == null)
                return;

            var embed = GuildLogEmbeds.MessageEdited(
                after.Author?.GetDisplayName() ?? BotConstants.Unknown,
                beforeContent,
                after.Content,
                after.Channel.Name);

            await logChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar edicao de mensagem");
        }
    }

    public override async Task OnMessagesBulkDeletedAsync(
        IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
        Cacheable<IMessageChannel, ulong> channel)
    {
        try
        {
            if (!channel.HasValue || channel.Value is not SocketGuildChannel guildChannel)
                return;

            var settings = await _guildRepository.GetAsync(guildChannel.Guild.Id);
            var log = settings.Logs;
            if (log == null || !log.Enabled || !log.LogBulkDeletes || !log.ChannelId.HasValue)
                return;

            var logChannel = guildChannel.Guild.GetTextChannel(log.ChannelId.Value);
            if (logChannel == null)
                return;

            var embed = GuildLogEmbeds.MessagesBulkDeleted(messages.Count, guildChannel.Name);
            await logChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar exclusao em massa");
        }
    }

    public override async Task OnGuildMemberUpdatedAsync(
        Cacheable<SocketGuildUser, ulong> before,
        SocketGuildUser after)
    {
        try
        {
            var settings = await _guildRepository.GetAsync(after.Guild.Id);
            var log = settings.Logs;
            if (log == null || !log.Enabled || !log.ChannelId.HasValue)
                return;

            var prev = before.HasValue ? before.Value : null;
            if (prev == null)
                return;

            if (prev.Nickname != after.Nickname)
            {
                await TrySendModerationAsync(after.Guild, l => l.LogNicknameChanges,
                    GuildLogEmbeds.NicknameChanged(after.GetDisplayName(), after.Id, prev.Nickname, after.Nickname ?? string.Empty));
            }

            var addedRoles = after.Roles
                .Where(r => !before.Value.Roles.Any(x => x.Id == r.Id))
                .Select(r => r.Name)
                .ToList();
            var removedRoles = before.Value.Roles
                .Where(r => after.Roles.All(x => x.Id != r.Id))
                .Select(r => r.Name)
                .ToList();

            if (addedRoles.Count > 0 || removedRoles.Count > 0)
            {
                await TrySendModerationAsync(after.Guild, l => l.LogRoleChanges,
                    GuildLogEmbeds.RolesChanged(after.GetDisplayName(), after.Id, addedRoles, removedRoles));
            }

            if (prev.TimedOutUntil != after.TimedOutUntil)
            {
                if (after.TimedOutUntil.HasValue)
                {
                    await TrySendModerationAsync(after.Guild, l => l.LogTimeouts,
                        GuildLogEmbeds.TimeoutApplied(after.GetDisplayName(), after.Id, after.TimedOutUntil.Value));
                }
                else
                {
                    await TrySendModerationAsync(after.Guild, l => l.LogTimeouts,
                        GuildLogEmbeds.TimeoutRemoved(after.GetDisplayName(), after.Id));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar alteracao de membro");
        }
    }

    public override async Task OnUserBannedAsync(SocketUser user, SocketGuild guild)
    {
        await TrySendModerationAsync(guild, log => log.LogBans,
            GuildLogEmbeds.UserBanned(user.GetDisplayName(), user.Id, user.CreatedAt, user.IsBot, user.GetAvatarUrl()));
    }

    public override async Task OnUserUnbannedAsync(SocketUser user, SocketGuild guild)
    {
        await TrySendModerationAsync(guild, log => log.LogBans,
            GuildLogEmbeds.UserUnbanned(user.GetDisplayName(), user.Id, user.CreatedAt, user.IsBot, user.GetAvatarUrl()));
    }

    public override async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        await TrySendMembersAsync(user.Guild,
            GuildLogEmbeds.UserJoined(user.GetDisplayName(), user.Guild.MemberCount, user.CreatedAt, user.IsBot, user.GetAvatarUrl()));
    }

    public override async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        try
        {
            PruneKickMarkers();
            var utcNow = DateTimeOffset.UtcNow;

            if (_kickedMarkers.TryRemove((guild.Id, user.Id), out var marker)
                && utcNow - marker.At <= KickMarkerTtl)
            {
                await TrySendModerationAsync(guild, log => log.LogKicks,
                    GuildLogEmbeds.UserKicked(user.GetDisplayName(), user.Id, user.CreatedAt, user.IsBot, user.GetAvatarUrl(), marker.Reason));
                return;
            }

            await TrySendMembersAsync(guild,
                GuildLogEmbeds.UserLeft(user.GetDisplayName(), guild.MemberCount, user.CreatedAt, user.IsBot, user.GetAvatarUrl()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar saida no servidor {guildId}", guild.Id);
        }
    }

    public override async Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        try
        {
            if (user.IsBot)
                return;

            var joined = after.VoiceChannel != null && before.VoiceChannel == null;
            var left = before.VoiceChannel != null && after.VoiceChannel == null;
            if (!joined && !left)
                return;

            var voiceChannel = joined ? after.VoiceChannel : before.VoiceChannel;
            if (voiceChannel == null || voiceChannel.Guild is not SocketGuild guild)
                return;

            await TrySendModerationAsync(guild, log => log.LogVoice,
                GuildLogEmbeds.VoiceStateChanged(user.Username, user.Id, voiceChannel.Name, joined));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar estado de voz");
        }
    }

    public override async Task OnChannelCreatedAsync(SocketChannel channel)
    {
        await LogChannelEventAsync(channel, GuildLogEmbeds.ChannelCreated);
    }

    public override async Task OnChannelDestroyedAsync(SocketChannel channel)
    {
        await LogChannelEventAsync(channel, GuildLogEmbeds.ChannelDestroyed);
    }

    public override async Task OnChannelUpdatedAsync(SocketChannel before, SocketChannel after)
    {
        await LogChannelEventAsync(after, GuildLogEmbeds.ChannelUpdated);
    }

    private async Task LogChannelEventAsync(SocketChannel channel, Func<string, ulong, string, Embed> build)
    {
        try
        {
            if (channel is not SocketGuildChannel guildChannel)
                return;

            await TrySendModerationAsync(guildChannel.Guild, log => log.LogChannelChanges,
                build(guildChannel.Name, guildChannel.Id, ChannelTypeName(guildChannel)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar evento de canal");
        }
    }

    private static string ChannelTypeName(SocketGuildChannel channel) => channel switch
    {
        SocketCategoryChannel => "Categoria",
        SocketThreadChannel => "Thread",
        SocketForumChannel => "Fórum",
        SocketVoiceChannel => "Voz",
        SocketTextChannel => "Texto",
        _ => "Outro"
    };

    private void PruneKickMarkers()
    {
        var cutoff = DateTimeOffset.UtcNow - KickMarkerTtl;
        foreach (var key in _kickedMarkers.Keys)
        {
            if (_kickedMarkers.TryGetValue(key, out var marker) && marker.At < cutoff)
                _kickedMarkers.TryRemove(key, out _);
        }
    }

    private async Task TrySendModerationAsync(SocketGuild guild, Func<GuildLogSettings, bool> shouldLog, Embed embed)
    {
        try
        {
            var logChannel = await GetLogChannelAsync(guild, log => log.Enabled && shouldLog(log));
            if (logChannel == null)
                return;
            await logChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar moderacao no servidor {guildId}", guild.Id);
        }
    }

    private async Task TrySendMembersAsync(SocketGuild guild, Embed embed)
    {
        try
        {
            var logChannel = await GetLogChannelAsync(guild, log => log.LogMembers);
            if (logChannel == null)
                return;
            await logChannel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar membro no servidor {guildId}", guild.Id);
        }
    }

    private async Task<ITextChannel?> GetLogChannelAsync(SocketGuild guild, Func<GuildLogSettings, bool> shouldLog)
    {
        try
        {
            var settings = await _guildRepository.GetAsync(guild.Id);
            var log = settings.Logs;
            if (log == null || !log.Enabled || !log.ChannelId.HasValue || !shouldLog(log))
                return null;

            return guild.GetTextChannel(log.ChannelId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter canal de log do servidor {guildId}", guild.Id);
            return null;
        }
    }
}