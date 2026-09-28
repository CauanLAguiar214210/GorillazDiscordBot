using System.Collections.Concurrent;
using System.Collections.Immutable;
using GorillazDiscordBot.Configuration;
using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Preconditions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Services;

public class AudioPlayerService : IAudioPlayerService
{
    private readonly IAudioService _audio;
    private readonly IOptions<LavalinkOptions> _lavalinkOptions;
    private readonly ILogger<AudioPlayerService> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _playLocks = new();
    private readonly object _eventAttachLock = new();
    private bool _eventsAttached;

    public AudioPlayerService(
        IAudioService audio,
        IOptions<LavalinkOptions> lavalinkOptions,
        ILogger<AudioPlayerService> logger)
    {
        _audio = audio;
        _lavalinkOptions = lavalinkOptions;
        _logger = logger;
    }

    public AudioTrackResolveResult Resolve(string? origin)
        => AudioTrackResolver.Resolve(origin, _lavalinkOptions.Value.LocalAudioPath);

    public async Task<AudioPlayResult> PlayAsync(
        ulong guildId,
        ulong voiceChannelId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _playLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            AttachEvents();
            await StopExistingAsync(guildId, cancellationToken);

            try
            {
                await _audio.WaitForReadyAsync(cancellationToken)
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Servidor de música (Lavalink) ainda não está pronto");
                return new AudioPlayResult(false, "Servidor de música (Lavalink) indisponível. Tente novamente em alguns instantes.");
            }

            var retrieve = await _audio.Players.RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                guildId,
                voiceChannelId,
                PlayerFactory.Default,
                Options.Create(new LavalinkPlayerOptions
                {
                    DisconnectOnStop = false,
                    DisconnectOnDestroy = false,
                    SelfDeaf = false
                }),
                new PlayerRetrieveOptions(
                    PlayerChannelBehavior.Join,
                    MemberVoiceStateBehavior.Ignore,
                    ImmutableArray<IPlayerPrecondition>.Empty,
                    null),
                cancellationToken);

            if (!retrieve.IsSuccess)
            {
                _logger.LogInformation("Falha ao obter player da guilda {guildId}: {status}", guildId, retrieve.Status);
                return new AudioPlayResult(false, $"Não consegui entrar no canal de voz ({retrieve.Status}).");
            }

            try
            {
                await retrieve.Player.PlayAsync(
                    AudioTrackResolver.ToServerIdentifier(identifier),
                    new TrackPlayProperties(),
                    cancellationToken);
                return new AudioPlayResult(true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao iniciar reprodução de {identifier} na guilda {guildId}", identifier, guildId);
                return new AudioPlayResult(false, "Não consegui iniciar a reprodução do áudio (servidor de música indisponível?).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reproduzir áudio {identifier} na guilda {guildId}", identifier, guildId);
            return new AudioPlayResult(false, "Ocorreu um erro ao reproduzir o áudio.");
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<AudioStopResult> StopAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (!_audio.Players.TryGetPlayer(guildId, out var player))
            return new AudioStopResult(false, true);

        try
        {
            await player.StopAsync(cancellationToken);
            await player.DisconnectAsync(cancellationToken);
            return new AudioStopResult(true, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao parar áudio na guilda {guildId}", guildId);
            return new AudioStopResult(true, false);
        }
    }

    private async Task StopExistingAsync(ulong guildId, CancellationToken cancellationToken)
    {
        if (!_audio.Players.TryGetPlayer(guildId, out var player))
            return;

        try
        {
            await player.StopAsync(cancellationToken);
            await player.DisconnectAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Player já desconectado — nada a fazer.
        }
    }

    private void AttachEvents()
    {
        if (_eventsAttached)
            return;

        lock (_eventAttachLock)
        {
            if (_eventsAttached)
                return;

            _audio.TrackEnded += OnTrackEndedAsync;
            _audio.TrackException += OnTrackExceptionAsync;
            _audio.TrackStuck += OnTrackStuckAsync;
            _eventsAttached = true;
        }
    }

    private Task OnTrackEndedAsync(object? sender, TrackEndedEventArgs e)
        => FinishSessionAsync(e.Player, $"áudio terminou ({e.Reason})");

    private Task OnTrackExceptionAsync(object? sender, TrackExceptionEventArgs e)
        => FinishSessionAsync(e.Player, "erro ao tocar áudio");

    private Task OnTrackStuckAsync(object? sender, TrackStuckEventArgs e)
        => FinishSessionAsync(e.Player, "áudio travou");

    private async Task FinishSessionAsync(ILavalinkPlayer player, string motivo)
    {
        _logger.LogInformation("Toca-e-sai: player da guilda {guildId} desconectando ({motivo})", player.GuildId, motivo);

        try
        {
            await player.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao desconectar player (já desconectado?)");
        }
    }
}