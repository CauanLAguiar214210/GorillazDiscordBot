using System.Collections.Concurrent;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public interface IVoicePresenceService
{
    Task<(bool Success, string? Error)> JoinAsync(IGuild guild, IVoiceChannel channel);
    Task LeaveAsync(SocketGuild guild);
    bool IsConnected(ulong guildId);
    IAudioClient? GetAudioClient(ulong guildId);
    Task OnBotVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after);
    Task ReconnectAllAsync();
}

public class VoicePresenceService : IVoicePresenceService
{
    private static readonly TimeSpan RejoinDelay = TimeSpan.FromSeconds(3);
    private const int MaxJoinAttempts = 3;

    private readonly DiscordSocketClient _client;
    private readonly ISettingsRepository<GuildVoicePresenceSettings> _settingsRepository;
    private readonly ILogger<VoicePresenceService> _logger;
    private readonly ConcurrentDictionary<ulong, IAudioClient> _audioClients = new();
    private readonly ConcurrentDictionary<ulong, byte> _reconnecting = new();

    public VoicePresenceService(
        DiscordSocketClient client,
        ISettingsRepository<GuildVoicePresenceSettings> settingsRepository,
        ILogger<VoicePresenceService> logger)
    {
        _client = client;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> JoinAsync(IGuild guild, IVoiceChannel channel)
    {
        var botMember = await guild.GetUserAsync(_client.CurrentUser.Id, CacheMode.CacheOnly);
        if (botMember != null)
        {
            var permissions = botMember.GetPermissions(channel);
            if (!permissions.ViewChannel || !permissions.Connect)
                return (false, "estou sem permissão de **Ver canal** ou **Conectar** neste canal.");
        }

        for (var attempt = 1; attempt <= MaxJoinAttempts; attempt++)
        {
            try
            {
                var audioClient = await channel.ConnectAsync(selfDeaf: true);
                _audioClients[guild.Id] = audioClient;

                var settings = await _settingsRepository.GetAsync(guild.Id);
                settings.VoiceChannelId = channel.Id;
                settings.Enabled = true;
                await _settingsRepository.SaveAsync(settings);

                _logger.LogInformation("Bot conectado ao canal de voz {channel} em {guild}", channel.Name, guild.Name);
                return (true, null);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex,
                    "Tentativa {attempt}/{maxAttempts} de conexão de voz expirou em {channel} ({guild})",
                    attempt, MaxJoinAttempts, channel.Name, guild.Name);

                if (attempt == MaxJoinAttempts)
                {
                    return (false,
                        "a conexão de voz expirou. Verifique se as bibliotecas nativas de áudio estão instaladas " +
                        "(libsodium/opus), se o firewall permite tráfego UDP e tente novamente.");
                }

                await Task.Delay(attempt == 1 ? TimeSpan.FromSeconds(3) : TimeSpan.FromSeconds(8));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao conectar ao canal de voz {channel} em {guild}", channel.Name, guild.Name);
                return (false, ex.Message);
            }
        }

        return (false, "não foi possível conectar ao canal de voz.");
    }

    public async Task LeaveAsync(SocketGuild guild)
    {
        if (_audioClients.TryRemove(guild.Id, out var audioClient))
        {
            try
            {
                await audioClient.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao desconectar de voz no servidor {guild}", guild.Name);
            }
        }

        var settings = await _settingsRepository.GetAsync(guild.Id);
        settings.Enabled = false;
        settings.VoiceChannelId = null;
        await _settingsRepository.SaveAsync(settings);

        _logger.LogInformation("Bot desconectado do canal de voz em {guild}", guild.Name);
    }

    public bool IsConnected(ulong guildId) => _audioClients.ContainsKey(guildId);

    public IAudioClient? GetAudioClient(ulong guildId)
        => _audioClients.TryGetValue(guildId, out var audioClient) ? audioClient : null;

    public async Task OnBotVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        if (user.Id != _client.CurrentUser.Id) return;

        try
        {
            if (after.VoiceChannel != null &&
                (before.VoiceChannel == null || before.VoiceChannel.Id != after.VoiceChannel.Id))
            {
                await TrackMoveAsync(after.VoiceChannel);
                return;
            }

            if (after.VoiceChannel == null && before.VoiceChannel != null)
                _ = ScheduleRejoinAsync(before.VoiceChannel.Guild);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mudança de estado de voz do bot");
        }
    }

    public async Task ReconnectAllAsync()
    {
        foreach (var guild in _client.Guilds)
        {
            try
            {
                var settings = await _settingsRepository.GetAsync(guild.Id);
                if (!settings.Enabled || !settings.VoiceChannelId.HasValue) continue;
                if (_audioClients.ContainsKey(guild.Id)) continue;

                var channel = guild.GetVoiceChannel(settings.VoiceChannelId.Value);
                if (channel == null) continue;

                await JoinAsync(guild, channel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao reconectar voz no servidor {guild}", guild.Name);
            }
        }
    }

    private async Task TrackMoveAsync(IVoiceChannel channel)
    {
        var settings = await _settingsRepository.GetAsync(channel.Guild.Id);
        if (!settings.Enabled) return;
        if (settings.VoiceChannelId == channel.Id && _audioClients.ContainsKey(channel.Guild.Id)) return;

        settings.VoiceChannelId = channel.Id;
        await _settingsRepository.SaveAsync(settings);
    }

    private async Task ScheduleRejoinAsync(SocketGuild guild)
    {
        if (!_reconnecting.TryAdd(guild.Id, 0)) return;

        try
        {
            await Task.Delay(RejoinDelay);

            var settings = await _settingsRepository.GetAsync(guild.Id);
            if (!settings.Enabled || !settings.VoiceChannelId.HasValue) return;
            if (_audioClients.ContainsKey(guild.Id)) return;

            var channel = guild.GetVoiceChannel(settings.VoiceChannelId.Value);
            if (channel == null)
            {
                _logger.LogWarning("Canal de voz salvo não existe mais no servidor {guild}; presença desativada", guild.Name);
                settings.Enabled = false;
                settings.VoiceChannelId = null;
                await _settingsRepository.SaveAsync(settings);
                return;
            }

            var (success, error) = await JoinAsync(guild, channel);
            if (!success)
                _logger.LogWarning("Reconexão de voz falhou em {guild}: {error}", guild.Name, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tentar reconectar voz no servidor {guild}", guild.Name);
        }
        finally
        {
            _reconnecting.TryRemove(guild.Id, out _);
        }
    }
}
