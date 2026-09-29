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

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ILogger<JoinLeaveSoundService> _logger;
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, DateTime>> _lastPlayed = new();

    public JoinLeaveSoundService(
        ISettingsRepository<Guild> guildRepository,
        IAudioPlayerService audioPlayer,
        ILogger<JoinLeaveSoundService> logger)
    {
        _guildRepository = guildRepository;
        _audioPlayer = audioPlayer;
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
                await TryPlayAsync(guild.Id, before.VoiceChannel.Id, user.Id, settings.LeaveVoiceSound);

            if (after.VoiceChannel != null)
                await TryPlayAsync(guild.Id, after.VoiceChannel.Id, user.Id, settings.JoinVoiceSound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tocar som de entrada/saída de canal de voz");
        }
    }

    private async Task TryPlayAsync(ulong guildId, ulong voiceChannelId, ulong userId, string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        if (!IsOnCooldown(guildId, userId))
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

    private bool IsOnCooldown(ulong guildId, ulong userId)
    {
        var guildKeys = _lastPlayed.GetOrAdd(guildId, _ => new ConcurrentDictionary<ulong, DateTime>());
        var now = DateTime.UtcNow;
        var last = guildKeys.GetOrAdd(userId, now);

        if (now - last < TimeSpan.FromSeconds(CooldownSeconds))
            return false;

        guildKeys[userId] = now;
        return true;
    }
}