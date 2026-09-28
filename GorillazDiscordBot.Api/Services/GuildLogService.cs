using System.Text;
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
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly ILogger<GuildLogService> _logger;

    public GuildLogService(ISettingsRepository<Guild> guildRepository, ILogger<GuildLogService> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

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

    public override async Task OnUserBannedAsync(SocketUser user, SocketGuild guild)
    {
        await TrySendModerationAsync(guild, log => log.LogBans,
            GuildLogEmbeds.UserBanned(user.GetDisplayName(), user.Id));
    }

    public override async Task OnUserUnbannedAsync(SocketUser user, SocketGuild guild)
    {
        await TrySendModerationAsync(guild, log => log.LogBans,
            GuildLogEmbeds.UserUnbanned(user.GetDisplayName(), user.Id));
    }

    public override async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        await TrySendMembersAsync(user.Guild,
            GuildLogEmbeds.UserJoined(user.GetDisplayName(), user.Guild.MemberCount));
    }

    public override async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        await TrySendMembersAsync(guild,
            GuildLogEmbeds.UserLeft(user.GetDisplayName(), guild.MemberCount));
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