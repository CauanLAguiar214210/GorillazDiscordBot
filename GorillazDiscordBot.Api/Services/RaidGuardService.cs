using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Events;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class RaidGuardService : BotEventSink
{
    private sealed class RaidState
    {
        public bool Active;
        public DateTime LastJoinUtc;
        public HashSet<ulong> Banned = new();
    }

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IGuildMemberRepository _memberRepository;
    private readonly GuildLogService _guildLog;
    private readonly ILogger<RaidGuardService> _logger;
    private readonly ConcurrentDictionary<ulong, List<DateTime>> _joinLog = new();
    private readonly ConcurrentDictionary<ulong, RaidState> _raidStates = new();

    public RaidGuardService(
        ISettingsRepository<Guild> guildRepository,
        IGuildMemberRepository memberRepository,
        GuildLogService guildLog,
        ILogger<RaidGuardService> logger)
    {
        _guildRepository = guildRepository;
        _memberRepository = memberRepository;
        _guildLog = guildLog;
        _logger = logger;
    }

    public override async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            if (user.IsBot)
                return;

            var settings = (await _guildRepository.GetAsync(user.Guild.Id)).Raid;
            if (settings == null || !settings.Enabled)
                return;

            var window = TimeSpan.FromSeconds(Math.Max(5, settings.WindowSeconds));
            var now = DateTime.UtcNow;

            var timestamps = _joinLog.GetOrAdd(user.Guild.Id, _ => new List<DateTime>());
            timestamps.Add(now);
            timestamps.RemoveAll(t => t < now - window);

            var state = _raidStates.GetOrAdd(user.Guild.Id, _ => new RaidState());
            var burst = timestamps.Count > settings.MaxJoinsPerMinute;

            var wasActive = state.Active;
            state.Active = state.Active || burst;
            state.LastJoinUtc = now;

            if (!state.Active)
                return;

            if (!wasActive)
            {
                _logger.LogWarning("Possível raid detectado no servidor {guildId}: {count} entradas na janela",
                    user.Guild.Id, timestamps.Count);
                await _guildLog.RecordAsync(user.Guild, _ => true,
                    GuildLogEmbeds.RaidDetected(timestamps.Count, settings.MaxJoinsPerMinute));
            }

            if (state.Banned.Add(user.Id))
                await BanRaiderAsync(user, settings);

            if (!burst && now - state.LastJoinUtc > window * 2)
            {
                state.Active = false;
                state.Banned.Clear();
                timestamps.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no anti-raid ao processar entrada no servidor {guildId}", user.Guild.Id);
        }
    }

    private async Task BanRaiderAsync(SocketGuildUser user, RaidSettings settings)
    {
        if (!user.Guild.CurrentUser.GuildPermissions.Has(GuildPermission.BanMembers))
        {
            _logger.LogWarning("Anti-raid sem permissão de banir no servidor {guildId}", user.Guild.Id);
            return;
        }

        await user.Guild.AddBanAsync(user.Id, 0, $"Anti-raid: {settings.MaxJoinsPerMinute}+ entradas na janela");
        await _memberRepository.SetBanAsync(user.Guild.Id, user.Id, user.Username, true);
    }
}