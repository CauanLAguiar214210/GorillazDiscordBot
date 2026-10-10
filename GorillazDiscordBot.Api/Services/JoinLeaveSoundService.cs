using System.Collections.Concurrent;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Events;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class JoinLeaveSoundService : IBotEventSink
{
    private const int CooldownSeconds = 15;
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<JoinLeaveSoundService> _logger;
    private readonly ConcurrentDictionary<(ulong Guild, ulong User, SoundKind Kind), DateTime> _lastPlayed = new();
    private DateTime _nextPrune = DateTime.MinValue;

    public JoinLeaveSoundService(
        ISettingsRepository<Guild> guildRepository,
        IAudioPlayerService audioPlayer,
        DiscordSocketClient client,
        ILogger<JoinLeaveSoundService> logger)
    {
        _guildRepository = guildRepository;
        _audioPlayer = audioPlayer;
        _client = client;
        _logger = logger;
    }

    public void Subscribe(DiscordSocketClient client)
        => client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;

    public void Unsubscribe(DiscordSocketClient client)
        => client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;

    private async Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        if (user.IsBot)
            return;

        try
        {
            var current = before.VoiceChannel ?? after.VoiceChannel;
            if (current?.Guild is not SocketGuild guild)
                return;

            var settings = await _guildRepository.GetAsync(guild.Id);

            if (before.VoiceChannel != null && before.VoiceChannel.Id != after.VoiceChannel?.Id)
                await TryPlayAsync(guild.Id, before.VoiceChannel.Id, user.Id, settings.LeaveVoiceSound, SoundKind.Leave);

            if (after.VoiceChannel != null && after.VoiceChannel.Id != before.VoiceChannel?.Id)
                await TryPlayAsync(guild.Id, after.VoiceChannel.Id, user.Id, settings.JoinVoiceSound, SoundKind.Join);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tocar som de entrada/saída de canal de voz");
        }
    }

    private async Task TryPlayAsync(ulong guildId, ulong voiceChannelId, ulong userId, string? source, SoundKind kind)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        // Som de entrada/saída nunca puxa o bot para o canal: só toca se o bot já
        // estiver conectado nele (ou seja, depois que alguém usou `join`).
        var botChannelId = _client.GetGuild(guildId)?.CurrentUser?.VoiceChannel?.Id;
        if (botChannelId != voiceChannelId)
        {
            _logger.LogDebug(
                "Bot não está no canal {voiceChannelId} da guilda {guildId}; som de entrada/saída ignorado (sem join ativo).",
                voiceChannelId,
                guildId);
            return;
        }

        if (!TryStartCooldown(guildId, userId, kind))
            return;

        var resolved = await _audioPlayer.ResolveAsync(source);
        if (!resolved.IsValid)
        {
            _logger.LogWarning("Som de entrada/saída inválido na guilda {guildId}: {error}", guildId, resolved.Error);
            return;
        }

        var result = await _audioPlayer.PlayAsync(guildId, voiceChannelId, resolved.Identifier!);
        if (!result.Success)
            _logger.LogWarning("Falha ao tocar som de entrada/saída na guilda {guildId}: {error}", guildId, result.Error);
    }

    /// <summary>
    /// Reserva o slot de cooldown para o próximo som do usuário. Retorna <c>true</c>
    /// quando a reprodução é permitida. A chave inclui o tipo de som para que uma
    /// troca de canal (sai-entra) não suprima o som seguinte, e o primeiro evento
    /// de cada usuário é sempre permitido (ausência de registro = cooldown expirado).
    /// </summary>
    private bool TryStartCooldown(ulong guildId, ulong userId, SoundKind kind)
    {
        var now = DateTime.UtcNow;
        PruneExpired(now);

        while (true)
        {
            var key = (guildId, userId, kind);

            if (!_lastPlayed.TryGetValue(key, out var last))
            {
                if (_lastPlayed.TryAdd(key, now))
                    return true;
                continue;
            }

            if (now - last < TimeSpan.FromSeconds(CooldownSeconds))
                return false;

            if (_lastPlayed.TryUpdate(key, now, last))
                return true;
        }
    }

    /// <summary>
    /// Poda entradas cujo cooldown já expirou, com folga para não iterar a cada evento.
    /// Sem isso o dicionário cresce sem limite (um registro por usuário/guild/tipo para sempre).
    /// </summary>
    private void PruneExpired(DateTime now)
    {
        if (now < _nextPrune)
            return;

        _nextPrune = now + PruneInterval;

        foreach (var entry in _lastPlayed)
        {
            if (now - entry.Value >= TimeSpan.FromSeconds(CooldownSeconds))
                _lastPlayed.TryRemove(entry);
        }
    }

    private enum SoundKind
    {
        Join,
        Leave
    }
}