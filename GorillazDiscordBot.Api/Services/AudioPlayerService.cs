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
    private const int MaxStartAttempts = 2;
    private const int MaxErrorLength = 180;

    private readonly IAudioService _audio;
    private readonly IOptions<LavalinkOptions> _lavalinkOptions;
    private readonly IInstantSoundResolver _instantSounds;
    private readonly ILogger<AudioPlayerService> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _playLocks = new();
    private readonly ConcurrentDictionary<ulong, PendingStart> _pendingStarts = new();
    private readonly ConcurrentDictionary<ulong, ILavalinkPlayer> _activePlayers = new();
    private readonly ConcurrentDictionary<ulong, ulong> _persistentChannels = new();
    private readonly object _eventAttachLock = new();
    private bool _eventsAttached;

    public AudioPlayerService(
        IAudioService audio,
        IOptions<LavalinkOptions> lavalinkOptions,
        IInstantSoundResolver instantSounds,
        ILogger<AudioPlayerService> logger)
    {
        _audio = audio;
        _lavalinkOptions = lavalinkOptions;
        _instantSounds = instantSounds;
        _logger = logger;

        AttachEvents();
    }

    public async Task<AudioTrackResolveResult> ResolveAsync(string? origin, CancellationToken cancellationToken = default)
    {
        var resolved = AudioTrackResolver.Resolve(origin, _lavalinkOptions.Value.LocalAudioPath);
        if (!resolved.IsValid || resolved.Kind != AudioOriginKind.InstantButton)
            return resolved;

        var instant = await _instantSounds.ResolveAsync(resolved.Identifier!, cancellationToken);
        return instant.Success
            ? resolved with { Identifier = instant.AudioUrl }
            : new AudioTrackResolveResult(false, null, instant.Error, resolved.Kind);
    }

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
            return await StartTrackAsync(guildId, voiceChannelId, identifier, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new AudioPlayResult(false, "Reprodução cancelada.");
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

    public async Task<AudioJoinResult> JoinAsync(
        ulong guildId,
        ulong voiceChannelId,
        CancellationToken cancellationToken = default)
    {
        var semaphore = _playLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_persistentChannels.TryGetValue(guildId, out var currentChannel)
                && currentChannel == voiceChannelId
                && _audio.Players.TryGetPlayer(guildId, out _))
                return new AudioJoinResult(true, null);

            await StopExistingAsync(guildId, cancellationToken, disconnect: true);
            await _audio.WaitForReadyAsync(cancellationToken)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);

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
                return new AudioJoinResult(false, $"Não consegui entrar no canal de voz ({retrieve.Status}).");

            _persistentChannels[guildId] = voiceChannelId;
            _activePlayers[guildId] = retrieve.Player;
            return new AudioJoinResult(true, null);
        }
        catch (OperationCanceledException)
        {
            return new AudioJoinResult(false, "Entrada no canal cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao entrar no canal de voz da guilda {guildId}", guildId);
            return new AudioJoinResult(false, DescribeFailure(ex));
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<AudioStopResult> LeaveAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _persistentChannels.TryRemove(guildId, out _);
        return await StopAsync(guildId, cancellationToken);
    }

    public async Task<AudioStopResult> StopAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (!_audio.Players.TryGetPlayer(guildId, out var player))
        {
            ClearSession(guildId);
            return new AudioStopResult(false, true);
        }

        try
        {
            await player.StopAsync(cancellationToken);
            if (!_persistentChannels.ContainsKey(guildId))
                await player.DisconnectAsync(cancellationToken);
            return new AudioStopResult(true, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao parar áudio na guilda {guildId}", guildId);
            return new AudioStopResult(true, false);
        }
        finally
        {
            ClearSession(guildId);
        }
    }

    /// <summary>
    /// Traduz a exceção da biblioteca em uma mensagem honesta. Antes, qualquer falha
    /// na chamada <c>PlayAsync</c> acusava o plugin do YouTube — inclusive corrida de
    /// player reciclada e nó do Lavalink fora do ar.
    /// </summary>
    internal static string DescribeFailure(Exception exception) => exception switch
    {
        ObjectDisposedException =>
            "O player de áudio foi reciclado antes de começar. Tente novamente em instantes.",
        TimeoutException =>
            "O servidor de música demorou demais para responder. Verifique se o Lavalink está no ar.",
        HttpRequestException =>
            "Não consegui falar com o servidor de música (Lavalink). Ele pode estar reiniciando.",
        OperationCanceledException =>
            "A reprodução foi cancelada ou o servidor de música demorou demais.",
        _ => "Não consegui iniciar a reprodução do áudio."
    };

    internal static string DescribeStartFailure(bool isYouTube, string? error, TimeSpan timeout)
    {
        var detail = string.IsNullOrWhiteSpace(error) ? null : error.Trim();
        var message = detail is null
            ? $"O áudio não começou a tocar em {Math.Max(1, (int)timeout.TotalSeconds)}s — o servidor de música não respondeu a tempo."
            : $"O servidor de música não conseguiu abrir este áudio: {Truncate(detail, MaxErrorLength)}";

        // Pista plausível (e não acusação): o plugin do YouTube só é a causa quando
        // o nó realmente não conseguiu abrir a faixa.
        if (isYouTube)
            message += " Se for a primeira vez, confira `plugins.youtube.enabled: true` e o jar em `/opt/Lavalink/plugins`.";

        return message;
    }

    private async Task<AudioPlayResult> StartTrackAsync(
        ulong guildId,
        ulong voiceChannelId,
        string identifier,
        CancellationToken cancellationToken)
    {
        if (_persistentChannels.TryGetValue(guildId, out var persistentChannel))
            voiceChannelId = persistentChannel;

        for (var attempt = 1; attempt <= MaxStartAttempts; attempt++)
        {
            var persistent = _persistentChannels.ContainsKey(guildId);
            await StopExistingAsync(guildId, cancellationToken, disconnect: !persistent);

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

            ILavalinkPlayer? player = persistent && _activePlayers.TryGetValue(guildId, out var persistentPlayer)
                ? persistentPlayer
                : null;
            var pending = new PendingStart();

            if (player == null)
            {
                try
                {
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

                    player = retrieve.Player;
                }
                catch (Exception ex) when (IsRetryable(ex) && attempt < MaxStartAttempts)
                {
                    _logger.LogWarning(ex, "Player da guilda {guildId} foi reciclado na tentativa {attempt}; tentando de novo", guildId, attempt);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao obter player da guilda {guildId}", guildId);
                    return new AudioPlayResult(false, DescribeFailure(ex));
                }
            }

            var selectedPlayer = player!;
            pending.SetPlayer(selectedPlayer);
            _activePlayers[guildId] = selectedPlayer;
            _pendingStarts[guildId] = pending;

            try
            {
                await selectedPlayer.PlayAsync(
                    AudioTrackResolver.ToServerIdentifier(identifier),
                    new TrackPlayProperties(),
                    cancellationToken);
            }
            catch (Exception ex) when (IsRetryable(ex) && attempt < MaxStartAttempts)
            {
                _logger.LogWarning(ex, "Player da guilda {guildId} foi reciclado durante o play na tentativa {attempt}; tentando de novo", guildId, attempt);
                ClearSession(guildId);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao iniciar reprodução de {identifier} na guilda {guildId}", identifier, guildId);
                ClearSession(guildId);
                return new AudioPlayResult(false, DescribeFailure(ex));
            }

            var result = await WaitForStartAsync(guildId, identifier, pending, cancellationToken);

            // Se nada começou a tocar, o bot não pode ficar conectado em silêncio.
            if (!result.Success)
                await FinishSessionAsync(selectedPlayer, "falha ao iniciar a reprodução");

            return result;
        }

        return new AudioPlayResult(false, "Não consegui iniciar a reprodução do áudio.");
    }

    private async Task<AudioPlayResult> WaitForStartAsync(
        ulong guildId,
        string identifier,
        PendingStart pending,
        CancellationToken cancellationToken)
    {
        var timeout = StartTimeout();

        try
        {
            var outcome = await pending.Completion.WaitAsync(timeout, cancellationToken);
            return outcome.Started
                ? new AudioPlayResult(true, null)
                : new AudioPlayResult(false, DescribeStartFailure(AudioTrackResolver.IsYouTubeRequest(identifier), outcome.Error, timeout));
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout de {timeout} aguardando TrackStarted para {identifier} na guilda {guildId}", timeout, identifier, guildId);
            return new AudioPlayResult(false, DescribeStartFailure(AudioTrackResolver.IsYouTubeRequest(identifier), null, timeout));
        }
        catch (OperationCanceledException)
        {
            return new AudioPlayResult(false, "Reprodução cancelada.");
        }
        finally
        {
            // Só a pendência de início é encerrada aqui. O player segue registrado como
            // sessão ativa para que o disconnect do toca-e-sai (TrackEnded) ainda
            // seja reconhecido como a sessão atual.
            ClearPending(guildId);
        }
    }

    private TimeSpan StartTimeout()
    {
        var configured = _lavalinkOptions.Value.PlaybackStartTimeoutSeconds;
        return TimeSpan.FromSeconds(configured is > 0 and <= 120 ? configured : LavalinkOptions.DefaultPlaybackStartTimeoutSeconds);
    }

    /// <summary>
    /// Só o player reciclado (destruído pelo Lavalink entre o retrieve e o play) vale
    /// uma nova tentativa. Qualquer outro erro é reportado, não mascarado com retry.
    /// </summary>
    private static bool IsRetryable(Exception exception)
        => exception is ObjectDisposedException;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private void ClearPending(ulong guildId)
    {
        _pendingStarts.TryRemove(guildId, out _);
    }

    private void ClearSession(ulong guildId)
    {
        ClearPending(guildId);
        _activePlayers.TryRemove(guildId, out _);
    }

    private async Task StopExistingAsync(
        ulong guildId,
        CancellationToken cancellationToken,
        bool disconnect)
    {
        if (!_audio.Players.TryGetPlayer(guildId, out var player))
            return;

        try
        {
            await player.StopAsync(cancellationToken);
            if (disconnect)
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

            _audio.TrackStarted += OnTrackStartedAsync;
            _audio.TrackEnded += OnTrackEndedAsync;
            _audio.TrackException += OnTrackExceptionAsync;
            _audio.TrackStuck += OnTrackStuckAsync;
            _eventsAttached = true;
        }
    }

    private Task OnTrackStartedAsync(object? sender, TrackStartedEventArgs e)
    {
        if (_pendingStarts.TryGetValue(e.Player.GuildId, out var pending) && ReferenceEquals(pending.Player, e.Player))
            pending.Succeed();

        return Task.CompletedTask;
    }

    private Task OnTrackEndedAsync(object? sender, TrackEndedEventArgs e)
        => FinishSessionAsync(e.Player, $"áudio terminou ({e.Reason})");

    private Task OnTrackExceptionAsync(object? sender, TrackExceptionEventArgs e)
    {
        if (_pendingStarts.TryGetValue(e.Player.GuildId, out var pending) && ReferenceEquals(pending.Player, e.Player))
            pending.Fail(e.Exception.Message);

        _logger.LogWarning(
            "Lavalink falhou ao tocar a faixa na guilda {guildId}: {error}",
            e.Player.GuildId,
            e.Exception.Message);

        return FinishSessionAsync(e.Player, "erro ao tocar áudio");
    }

    private Task OnTrackStuckAsync(object? sender, TrackStuckEventArgs e)
        => FinishSessionAsync(e.Player, "áudio travou");

    private async Task FinishSessionAsync(ILavalinkPlayer player, string motivo)
    {
        // Eventos de uma sessão anterior não podem derrubar a reprodução atual: o
        // DisconnectAsync envia voice-disconnect para a guilda inteira.
        if (!_activePlayers.TryGetValue(player.GuildId, out var active) || !ReferenceEquals(active, player))
        {
            _logger.LogDebug("Ignorando {motivo} da guilda {guildId}: player de sessão anterior", motivo, player.GuildId);
            return;
        }

        var persistent = _persistentChannels.ContainsKey(player.GuildId);
        _logger.LogInformation(
            persistent
                ? "Sessão persistente da guilda {guildId} encerrou a faixa ({motivo})"
                : "Toca-e-sai: player da guilda {guildId} desconectando ({motivo})",
            player.GuildId,
            motivo);

        ClearSession(player.GuildId);

        try
        {
            if (!persistent)
                await player.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao desconectar player (já desconectado?)");
        }
    }

    private sealed class PendingStart
    {
        private readonly TaskCompletionSource<StartOutcome> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ILavalinkPlayer? Player { get; private set; }

        public Task<StartOutcome> Completion => _completion.Task;

        public void SetPlayer(ILavalinkPlayer player) => Player = player;

        public void Succeed() => _completion.TrySetResult(StartOutcome.Ok);

        public void Fail(string? error) => _completion.TrySetResult(StartOutcome.WithError(error));
    }

    private readonly record struct StartOutcome(bool Started, string? Error)
    {
        public static StartOutcome Ok => new(true, null);

        public static StartOutcome WithError(string? error) => new(false, error);
    }
}
