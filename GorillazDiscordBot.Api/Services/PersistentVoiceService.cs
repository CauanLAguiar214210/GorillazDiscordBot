using System.Collections.Concurrent;
using Discord.WebSocket;
using GorillazDiscordBot.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public interface IPersistentVoiceService
{
    Task<AudioJoinResult> JoinAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken = default);

    Task<AudioStopResult> LeaveAsync(ulong guildId, CancellationToken cancellationToken = default);
}

public sealed class PersistentVoiceService : IPersistentVoiceService, IBotEventSink, IHostedService
{
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(15);

    private readonly DiscordSocketClient _client;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ILogger<PersistentVoiceService> _logger;
    private readonly ConcurrentDictionary<ulong, ulong> _channels = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;

    public PersistentVoiceService(
        DiscordSocketClient client,
        IAudioPlayerService audioPlayer,
        ILogger<PersistentVoiceService> logger)
    {
        _client = client;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitorTask = MonitorAsync(_monitorCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_monitorCancellation == null)
            return;

        _monitorCancellation.Cancel();
        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask;
            }
            catch (OperationCanceledException)
            {
                // Encerramento normal do monitor.
            }
        }

        _monitorCancellation.Dispose();
        _monitorCancellation = null;
        _monitorTask = null;
    }

    public void Subscribe(DiscordSocketClient client)
        => client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;

    public void Unsubscribe(DiscordSocketClient client)
        => client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;

    public async Task<AudioJoinResult> JoinAsync(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            var result = await _audioPlayer.JoinAsync(guildId, channelId, cancellationToken);
            if (result.Success)
                _channels[guildId] = channelId;

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AudioStopResult> LeaveAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var gate = _locks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            _channels.TryRemove(guildId, out _);
            return await _audioPlayer.LeaveAsync(guildId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        if (user is not SocketGuildUser guildUser || !_channels.ContainsKey(guildUser.Guild.Id))
            return;

        await CheckGuildAsync(guildUser.Guild, CancellationToken.None);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(MonitorInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            PruneLocks();

            foreach (var guildId in _channels.Keys)
            {
                var guild = _client.GetGuild(guildId);
                if (guild != null)
                {
                    try
                    {
                        await CheckGuildAsync(guild, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao verificar canal persistente da guilda {guildId}", guildId);
                    }
                }
            }
        }
    }

    private void PruneLocks()
    {
        foreach (var (guildId, gate) in _locks)
        {
            // Só poda locks ociosos e de guildas sem canal persistente; remover um
            // gate em uso (ou de guilda ativa) abriria uma segunda instância.
            if (gate.CurrentCount == 1 && !_channels.ContainsKey(guildId))
                _locks.TryRemove(guildId, out _);
        }
    }

    private async Task CheckGuildAsync(SocketGuild guild, CancellationToken cancellationToken)
    {
        if (!_channels.TryGetValue(guild.Id, out var channelId))
            return;

        var channel = guild.GetVoiceChannel(channelId);
        if (channel == null)
        {
            _logger.LogInformation(
                "Canal persistente da guilda {guildId} indisponível; bot saindo",
                guild.Id);
            await LeaveAsync(guild.Id, cancellationToken);
            return;
        }

        if (!channel.ConnectedUsers.Any(u => u.Id == _client.CurrentUser.Id))
        {
            // Disconnect externo: um admin derrubou o bot sem passar por LeaveAsync.
            // Não reconectamos (não se luta contra o admin) — só registramos.
            _logger.LogInformation(
                "Bot desconectado externamente do canal persistente {channelId} da guilda {guildId}; registro removido",
                channelId,
                guild.Id);
            await LeaveAsync(guild.Id, cancellationToken);
            return;
        }

        if (channel.ConnectedUsers.All(user => user.IsBot))
        {
            _logger.LogInformation(
                "Canal persistente da guilda {guildId} vazio; bot saindo",
                guild.Id);
            await LeaveAsync(guild.Id, cancellationToken);
        }
    }
}
