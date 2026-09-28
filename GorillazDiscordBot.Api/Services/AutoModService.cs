using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Events;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class AutoModService : BotEventSink
{
    private const int FloodWindowFloorSeconds = 60;

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IGuildMemberRepository _memberRepository;
    private readonly StrikeEnforcementService _strikeEnforcement;
    private readonly ILogger<AutoModService> _logger;
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId), List<DateTime>> _messageLog = new();

    public AutoModService(
        ISettingsRepository<Guild> guildRepository,
        IGuildMemberRepository memberRepository,
        StrikeEnforcementService strikeEnforcement,
        ILogger<AutoModService> logger)
    {
        _guildRepository = guildRepository;
        _memberRepository = memberRepository;
        _strikeEnforcement = strikeEnforcement;
        _logger = logger;
    }

    public override async Task OnMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            if (message.Author.IsBot || message.Author.IsWebhook)
                return;
            if (message.Channel is not SocketGuildChannel guildChannel)
                return;
            if (string.IsNullOrWhiteSpace(message.Content))
                return;

            var guild = await _guildRepository.GetAsync(guildChannel.Guild.Id);
            var settings = guild.Automod ?? new AutomodSettings();
            if (!settings.Enabled)
                return;

            var utcNow = DateTime.UtcNow;

            if (AutoModRules.ContainsBlockedWord(message.Content, settings.BlockedWords, out var match))
            {
                await EnforceAsync(message, guildChannel.Guild, settings,
                    new AutoModVerdict(true, settings.Action, $"palavra bloqueada: {match}"));
                return;
            }

            if (settings.BlockInvites && AutoModRules.ContainsInvite(message.Content))
            {
                await EnforceAsync(message, guildChannel.Guild, settings,
                    new AutoModVerdict(true, settings.Action, "convite bloqueado"));
                return;
            }

            if (settings.BlockEveryonePings && AutoModRules.MentionsEveryone(message.Content))
            {
                await EnforceAsync(message, guildChannel.Guild, settings,
                    new AutoModVerdict(true, settings.Action, "menção a @everyone/@here bloqueada"));
                return;
            }

            if (settings.MaxMentionsPerMessage > 0
                && AutoModRules.CountMentions(message.Content) > settings.MaxMentionsPerMessage)
            {
                await EnforceAsync(message, guildChannel.Guild, settings,
                    new AutoModVerdict(true, settings.Action,
                        $"excesso de menções ({settings.MaxMentionsPerMessage}+)"));
                return;
            }

            var history = TrackTimestamp(guildChannel.Guild.Id, message.Author.Id, utcNow, settings.IntervalSeconds);
            if (AutoModRules.IsFlooding(settings, history, utcNow))
            {
                await EnforceAsync(message, guildChannel.Guild, settings,
                    new AutoModVerdict(true, settings.Action,
                        $"flood ({settings.MaxMessagesPerInterval}+ mensagens em {settings.IntervalSeconds}s)"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no auto-mod ao processar mensagem {id}", message.Id);
        }
    }

    private List<DateTime> TrackTimestamp(ulong guildId, ulong userId, DateTime utcNow, int intervalSeconds)
    {
        var window = TimeSpan.FromSeconds(Math.Max(FloodWindowFloorSeconds, intervalSeconds));
        var history = _messageLog.GetOrAdd((guildId, userId), static _ => new List<DateTime>(capacity: 64));
        history.Add(utcNow);
        history.RemoveAll(t => t < utcNow - window);

        if (history.Count == 0)
            _messageLog.TryRemove((guildId, userId), out _);

        return history;
    }

    private async Task EnforceAsync(SocketMessage message, SocketGuild guild, AutomodSettings settings, AutoModVerdict verdict)
    {
        if (!verdict.ShouldAct)
            return;

        await DeleteMessageIfAllowedAsync(message, guild);

        if (verdict.Action == AutomodAction.Timeout
            && settings.TimeoutMinutes > 0
            && message.Author is IGuildUser guildUser
            && guildUser.Id != guild.OwnerId)
        {
            await ApplyTimeoutAsync(guild, guildUser, settings.TimeoutMinutes);
        }

        try
        {
            await _memberRepository.AddWarningAsync(
                guild.Id,
                message.Author.Id,
                message.Author.GetDisplayName(),
                new UserWarning
                {
                    Reason = $"Automod: {verdict.Reason}",
                    AddedBy = guild.CurrentUser.Id,
                    CreatedAt = DateTime.UtcNow
                });

            var member = await _memberRepository.GetAsync(guild.Id, message.Author.Id);
            var total = member?.Warnings.Count ?? 1;
            await _strikeEnforcement.EvaluateAsync(guild, message.Author.Id, message.Author.GetDisplayName(), total);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao registrar aviso de automod para {user}", message.Author.GetDisplayName());
        }
    }

    private async Task DeleteMessageIfAllowedAsync(SocketMessage message, SocketGuild guild)
    {
        if (!guild.CurrentUser.GuildPermissions.Has(GuildPermission.ManageMessages))
            return;
        if (message.IsPinned)
            return;

        try
        {
            await message.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover mensagem por automod {id}", message.Id);
        }
    }

    private async Task ApplyTimeoutAsync(SocketGuild guild, IGuildUser guildUser, int timeoutMinutes)
    {
        try
        {
            var timeout = TimeSpan.FromMinutes(timeoutMinutes);
            await guildUser.SetTimeOutAsync(timeout);
            await _memberRepository.SetMuteAsync(
                guild.Id,
                guildUser.Id,
                guildUser.GetDisplayName(),
                DateTime.UtcNow + timeout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao aplicar timeout por automod em {user}",
                guildUser.GetDisplayName());
        }
    }
}