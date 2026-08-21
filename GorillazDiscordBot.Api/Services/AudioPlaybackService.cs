using System.Collections.Concurrent;
using System.Diagnostics;
using Discord.Audio;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public interface IAudioPlaybackService
{
    void Enqueue(ulong guildId, GuildSoundInteraction sound);
    bool SkipCurrent(ulong guildId);
    int GetQueueLength(ulong guildId);
}

public class AudioPlaybackService : IAudioPlaybackService
{
    private const int VoiceReadBufferSize = 3840;
    private const int StdInBufferSize = 81920;

    private readonly IVoicePresenceService _voicePresence;
    private readonly IAudioFileStorage _audioStorage;
    private readonly ILogger<AudioPlaybackService> _logger;
    private readonly ConcurrentDictionary<ulong, GuildPlaybackState> _states = new();

    public AudioPlaybackService(
        IVoicePresenceService voicePresence,
        IAudioFileStorage audioStorage,
        ILogger<AudioPlaybackService> logger)
    {
        _voicePresence = voicePresence;
        _audioStorage = audioStorage;
        _logger = logger;
    }

    public void Enqueue(ulong guildId, GuildSoundInteraction sound)
    {
        var state = _states.GetOrAdd(guildId, _ => new GuildPlaybackState());
        state.Queue.Enqueue(sound);

        lock (state.Lock)
        {
            if (state.IsPlaying) return;
            state.IsPlaying = true;
        }

        _ = Task.Run(() => PlayLoopAsync(guildId, state));
    }

    public bool SkipCurrent(ulong guildId)
    {
        if (!_states.TryGetValue(guildId, out var state)) return false;

        lock (state.Lock)
        {
            if (!state.IsPlaying) return false;
            state.CurrentCts?.Cancel();
        }

        return true;
    }

    public int GetQueueLength(ulong guildId)
        => _states.TryGetValue(guildId, out var state) ? state.Queue.Count : 0;

    private async Task PlayLoopAsync(ulong guildId, GuildPlaybackState state)
    {
        try
        {
            while (state.Queue.TryDequeue(out var sound))
            {
                using var cts = new CancellationTokenSource();
                state.CurrentCts = cts;
                try
                {
                    await PlayAsync(guildId, sound, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Reprodução de '{trigger}' interrompida no servidor {guildId}",
                        sound.Trigger, guildId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao reproduzir '{trigger}' no servidor {guildId}",
                        sound.Trigger, guildId);
                }
            }
        }
        finally
        {
            lock (state.Lock)
            {
                state.IsPlaying = false;
                state.CurrentCts = null;
            }
        }
    }

    private async Task PlayAsync(ulong guildId, GuildSoundInteraction sound, CancellationToken cancellationToken)
    {
        var audioClient = _voicePresence.GetAudioClient(guildId);
        if (audioClient == null)
        {
            _logger.LogWarning("Bot não está em canal de voz no servidor {guildId}; som '{trigger}' descartado",
                guildId, sound.Trigger);
            return;
        }

        await using var fileStream = await _audioStorage.OpenReadAsync(sound.GridFsId);
        if (fileStream == null)
        {
            _logger.LogWarning("Arquivo de áudio {fileId} não encontrado para o som '{trigger}'",
                sound.GridFsId, sound.Trigger);
            return;
        }

        using var audioData = new MemoryStream();
        await fileStream.CopyToAsync(audioData, cancellationToken);
        audioData.Position = 0;

        using var ffmpeg = CreateFfmpegProcess();
        ffmpeg.BeginErrorReadLine();

        await using var standardInput = ffmpeg.StandardInput.BaseStream;
        await using var standardOutput = ffmpeg.StandardOutput.BaseStream;

        _ = FeedStdInAsync(audioData, standardInput, cancellationToken);

        await using var voiceStream = audioClient.CreatePCMStream(AudioApplication.Music);
        try
        {
            await standardOutput.CopyToAsync(voiceStream, VoiceReadBufferSize, cancellationToken);
            await voiceStream.FlushAsync(cancellationToken);
        }
        finally
        {
            KillIfRunning(ffmpeg);
        }
    }

    private static async Task FeedStdInAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        try
        {
            await source.CopyToAsync(destination, StdInBufferSize, cancellationToken);
            await destination.FlushAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            try
            {
                destination.Close();
            }
            catch (Exception)
            {
            }
        }
    }

    private static Process CreateFfmpegProcess() =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = "-hide_banner -loglevel error -i pipe:0 -vn -ac 2 -ar 48000 -f s16le pipe:1",
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Não foi possível iniciar o processo ffmpeg");

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
        }
    }
}

internal sealed class GuildPlaybackState
{
    public object Lock { get; } = new();
    public ConcurrentQueue<GuildSoundInteraction> Queue { get; } = new();
    public bool IsPlaying { get; set; }
    public CancellationTokenSource? CurrentCts { get; set; }
}
