using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Services;

public sealed record AudioTrackResolveResult(bool IsValid, string? Identifier, string? Error);

public sealed record AudioPlayResult(bool Success, string? Error);

public sealed record AudioStopResult(bool WasPlaying, bool Success);

public interface IAudioPlayerService
{
    AudioTrackResolveResult Resolve(string? origin);

    Task<AudioPlayResult> PlayAsync(
        ulong guildId,
        ulong voiceChannelId,
        string identifier,
        CancellationToken cancellationToken = default);

    Task<AudioStopResult> StopAsync(ulong guildId, CancellationToken cancellationToken = default);
}