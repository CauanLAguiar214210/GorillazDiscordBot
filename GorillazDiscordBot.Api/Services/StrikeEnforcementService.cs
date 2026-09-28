using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public sealed record StrikeOutcome(bool Banned, bool TimedOut, int WarningCount)
{
    public static StrikeOutcome None { get; } = new(false, false, 0);

    public static StrikeOutcome BannedAt(int warningCount) => new(true, false, warningCount);

    public static StrikeOutcome TimedOutAt(int warningCount) => new(false, true, warningCount);
}

public static class StrikePolicy
{
    public static bool ShouldBan(AutomodSettings settings, int warningCount)
        => settings.EnableStrikes
           && settings.StrikeBanWarnings > 0
           && warningCount >= settings.StrikeBanWarnings;

    public static bool ShouldTimeout(AutomodSettings settings, int warningCount)
        => settings.EnableStrikes
           && settings.StrikeTimeoutWarnings > 0
           && settings.StrikeTimeoutWarnings < settings.StrikeBanWarnings
           && warningCount == settings.StrikeTimeoutWarnings;
}

public class StrikeEnforcementService
{
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IGuildMemberRepository _memberRepository;
    private readonly ILogger<StrikeEnforcementService> _logger;

    public StrikeEnforcementService(
        ISettingsRepository<Guild> guildRepository,
        IGuildMemberRepository memberRepository,
        ILogger<StrikeEnforcementService> logger)
    {
        _guildRepository = guildRepository;
        _memberRepository = memberRepository;
        _logger = logger;
    }

    public async Task<StrikeOutcome> EvaluateAsync(SocketGuild guild, ulong targetId, string username, int warningCount)
    {
        try
        {
            var settings = (await _guildRepository.GetAsync(guild.Id)).Automod ?? new AutomodSettings();
            if (!settings.EnableStrikes)
                return StrikeOutcome.None;

            var target = guild.GetUser(targetId);
            if (target == null || target.IsBot)
                return StrikeOutcome.None;

            if (target.Id == guild.OwnerId || target.GuildPermissions.Administrator)
                return StrikeOutcome.None;

            if (StrikePolicy.ShouldBan(settings, warningCount))
            {
                await BanAsync(guild, target, username, settings.StrikeBanWarnings);
                return StrikeOutcome.BannedAt(warningCount);
            }

            if (StrikePolicy.ShouldTimeout(settings, warningCount))
            {
                await TimeoutAsync(guild, target, username, settings);
                return StrikeOutcome.TimedOutAt(warningCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao avaliar strikes do usuário {userId} no servidor {guildId}", targetId, guild.Id);
        }

        return StrikeOutcome.None;
    }

    private async Task BanAsync(SocketGuild guild, SocketGuildUser target, string username, int banWarnings)
    {
        await guild.AddBanAsync(target.Id, 0, $"Strike automático: {banWarnings} avisos");
        await _memberRepository.SetBanAsync(guild.Id, target.Id, username, true);
        _logger.LogInformation("Usuário {userId} banido por strikes no servidor {guildId}", target.Id, guild.Id);
    }

    private async Task TimeoutAsync(SocketGuild guild, SocketGuildUser target, string username, AutomodSettings settings)
    {
        var timeout = TimeSpan.FromMinutes(Math.Max(1, settings.TimeoutMinutes));
        await target.SetTimeOutAsync(timeout, RequestOptions.Default);
        await _memberRepository.SetMuteAsync(guild.Id, target.Id, username, DateTime.UtcNow + timeout);
        _logger.LogInformation("Usuário {userId} em timeout por strikes no servidor {guildId}", target.Id, guild.Id);
    }
}