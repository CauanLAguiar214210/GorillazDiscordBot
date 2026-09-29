using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Lavalink4NET;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class ScheduledSoundService : IHostedService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly DiscordSocketClient _client;
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IAudioService _audio;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ILogger<ScheduledSoundService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly HashSet<FiredKey> _fired = new();
    private Task? _task;

    public ScheduledSoundService(
        DiscordSocketClient client,
        ISettingsRepository<Guild> guildRepository,
        IAudioService audio,
        IAudioPlayerService audioPlayer,
        ILogger<ScheduledSoundService> logger)
    {
        _client = client;
        _guildRepository = guildRepository;
        _audio = audio;
        _audioPlayer = audioPlayer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _task = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        if (_task != null)
            await _task;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync(DateTime.UtcNow, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no tick de sons agendados");
            }

            try
            {
                await Task.Delay(TickInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task TickAsync(DateTime utcNow, CancellationToken ct)
    {
        if (_client.ConnectionState != ConnectionState.Connected)
            return;

        foreach (var socketGuild in _client.Guilds)
        {
            var guild = await _guildRepository.GetAsync(socketGuild.Id);
            if (guild.ScheduledSounds.Count == 0)
                continue;

            foreach (var schedule in guild.ScheduledSounds)
            {
                if (!ScheduleEvaluator.ShouldFire(schedule, utcNow))
                    continue;

                var key = new FiredKey(schedule.Id, utcNow.Date);
                if (!_fired.Add(key))
                    continue;

                await TryPlayAsync(guild, schedule, ct);
            }
        }

        PruneFired(utcNow);
    }

    private async Task TryPlayAsync(Guild guild, ScheduledSoundSettings schedule, CancellationToken ct)
    {
        try
        {
            var members = await _audio.DiscordClient.GetChannelUsersAsync(
                guild.GuildId, schedule.VoiceChannelId, false, ct);

            if (members.IsDefaultOrEmpty)
            {
                _logger.LogInformation("Áudio agendado {id} ignorado: canal de voz vazio", schedule.Id);
                return;
            }

            var resolved = await _audioPlayer.ResolveAsync(schedule.AudioSource, ct);
            if (!resolved.IsValid)
            {
                _logger.LogWarning("Áudio agendado {id} inválido e foi desativado: {error}", schedule.Id, resolved.Error);
                schedule.Enabled = false;
                await _guildRepository.SaveAsync(guild);
                return;
            }

            var result = await _audioPlayer.PlayAsync(guild.GuildId, schedule.VoiceChannelId, resolved.Identifier!, ct);
            if (result.Success)
            {
                schedule.TimesPlayed++;
                await _guildRepository.SaveAsync(guild);
                _logger.LogInformation("Áudio agendado {id} tocado ({times}x)", schedule.Id, schedule.TimesPlayed);
            }
            else
            {
                _logger.LogWarning("Áudio agendado {id} falhou: {error}", schedule.Id, result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao tocar áudio agendado {id}", schedule.Id);
        }
    }

    private void PruneFired(DateTime utcNow)
    {
        if (_fired.Count < 256)
            return;

        _fired.RemoveWhere(k => k.Date != utcNow.Date);
    }

    internal readonly record struct FiredKey(Guid Id, DateTime Date);
}