using Discord;
using GorillazDiscordBot.Domain.Entity.Release;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public sealed record ReleaseAnnounceResult(bool Sent, string? Reason);

public class ReleaseAnnouncementService
{
    private const int MaxReleasesPerRun = 20;

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IReleaseNoteRepository _releaseRepository;
    private readonly ILogger<ReleaseAnnouncementService> _logger;

    public ReleaseAnnouncementService(
        ISettingsRepository<Guild> guildRepository,
        IReleaseNoteRepository releaseRepository,
        ILogger<ReleaseAnnouncementService> logger)
    {
        _guildRepository = guildRepository;
        _releaseRepository = releaseRepository;
        _logger = logger;
    }

    public async Task<int> AnnouncePendingReleasesAsync(
        IReadOnlyCollection<IGuild> guilds,
        CancellationToken cancellationToken = default)
    {
        List<ReleaseNote> pending;
        try
        {
            pending = await _releaseRepository.GetUnannouncedAsync(DateTime.UtcNow, MaxReleasesPerRun, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao buscar releases pendentes de anúncio.");
            return 0;
        }

        if (pending.Count == 0)
            return 0;

        var targets = await ResolveTargetChannelsAsync(guilds);
        if (targets.Count == 0)
        {
            _logger.LogInformation(
                "Existem {count} release(s) pendentes, mas nenhum servidor tem canal de novidades configurado.",
                pending.Count);
            return 0;
        }

        var announced = 0;
        foreach (var release in pending)
        {
            try
            {
                if (!await _releaseRepository.TryMarkAnnouncedAsync(release.Version, DateTime.UtcNow, cancellationToken))
                {
                    _logger.LogInformation("Release {version} já foi anunciada por outra instância. Pulando.", release.Version);
                    continue;
                }

                foreach (var (guild, channel) in targets)
                    await SendToChannelAsync(guild, channel, release);

                announced++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao anunciar a release {version}.", release.Version);
            }
        }

        if (announced > 0)
            _logger.LogInformation("Releases anunciadas neste boot: {count}.", announced);

        return announced;
    }

    public async Task<ReleaseAnnounceResult> AnnounceToGuildAsync(
        IGuild guild,
        ReleaseNote release,
        CancellationToken cancellationToken = default)
    {
        var settings = await _guildRepository.GetAsync(guild.Id);
        if (!settings.Release.Enabled || !settings.Release.ChannelId.HasValue)
            return new ReleaseAnnounceResult(false, "Nenhum canal de novidades configurado. Use `/config release-canal`.");

        var channel = await guild.GetTextChannelAsync(settings.Release.ChannelId.Value, CacheMode.CacheOnly);
        if (channel == null)
            return new ReleaseAnnounceResult(false, "Não consegui encontrar o canal configurado (removido ou sem acesso).");

        var sent = await SendToChannelAsync(guild, channel, release);
        if (!sent)
            return new ReleaseAnnounceResult(false, "Não consegui enviar no canal configurado (sem permissão?).");

        if (release.AnnouncedAt == null)
            await _releaseRepository.TryMarkAnnouncedAsync(release.Version, DateTime.UtcNow, cancellationToken);

        return new ReleaseAnnounceResult(true, null);
    }

    private async Task<List<(IGuild Guild, ITextChannel Channel)>> ResolveTargetChannelsAsync(
        IReadOnlyCollection<IGuild> guilds)
    {
        var targets = new List<(IGuild Guild, ITextChannel Channel)>();
        foreach (var guild in guilds)
        {
            var settings = await _guildRepository.GetAsync(guild.Id);
            if (!settings.Release.Enabled || !settings.Release.ChannelId.HasValue)
                continue;

            var channel = await guild.GetTextChannelAsync(settings.Release.ChannelId.Value, CacheMode.CacheOnly);
            if (channel == null)
            {
                _logger.LogWarning(
                    "Canal de novidades {channelId} não encontrado no servidor {guild}.",
                    settings.Release.ChannelId.Value, guild.Name);
                continue;
            }

            targets.Add((guild, channel));
        }

        return targets;
    }

    private async Task<bool> SendToChannelAsync(
        IGuild guild,
        ITextChannel channel,
        ReleaseNote release)
    {
        try
        {
            await channel.SendMessageAsync(embed: ReleaseEmbedBuilder.BuildAnnouncement(release));
            _logger.LogInformation(
                "Release {version} anunciada no servidor {guild} (#{channel}).",
                release.Version, guild.Name, channel.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao anunciar a release {version} no servidor {guild}.", release.Version, guild.Name);
            return false;
        }
    }
}
