using System.Collections.Concurrent;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public interface ISoundTriggerService
{
    Task<bool> TryPlayAsync(SocketMessage message);
}

public class SoundTriggerService : ISoundTriggerService
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    private readonly ISoundInteractionRepository _soundRepository;
    private readonly IVoicePresenceService _voicePresence;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ILogger<SoundTriggerService> _logger;
    private readonly ConcurrentDictionary<ulong, DateTimeOffset> _lastPlayed = new();

    public SoundTriggerService(
        ISoundInteractionRepository soundRepository,
        IVoicePresenceService voicePresence,
        IAudioPlaybackService playbackService,
        ILogger<SoundTriggerService> logger)
    {
        _soundRepository = soundRepository;
        _voicePresence = voicePresence;
        _playbackService = playbackService;
        _logger = logger;
    }

    public async Task<bool> TryPlayAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return false;
        if (message.Channel is not SocketTextChannel textChannel) return false;

        var trigger = message.Content.Trim();
        if (string.IsNullOrEmpty(trigger) || trigger.Contains(' ')) return false;

        var guild = textChannel.Guild;
        if (guild == null) return false;
        if (!_voicePresence.IsConnected(guild.Id)) return false;

        var sound = await _soundRepository.GetAsync(guild.Id, trigger);
        if (sound == null) return false;

        var now = DateTimeOffset.UtcNow;
        if (_lastPlayed.TryGetValue(guild.Id, out var last) && now - last < Cooldown)
            return true;

        _lastPlayed[guild.Id] = now;
        _playbackService.Enqueue(guild.Id, sound);

        _logger.LogInformation("Som '{trigger}' enfileirado no servidor {guild} por {user}",
            sound.Trigger, guild.Name, message.Author.GetDisplayName());

        return true;
    }
}
