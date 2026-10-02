using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Services;

public enum AudioOriginKind
{
    Local,
    YouTube,
    YouTubeSearch,
    InstantButton,
    DirectUrl
}

public sealed record AudioTrackResolveResult(
    bool IsValid,
    string? Identifier,
    string? Error,
    AudioOriginKind Kind = AudioOriginKind.DirectUrl)
{
    public bool RequiresElevatedPermission => IsValid && Kind == AudioOriginKind.DirectUrl;
}

public sealed record AudioPlayResult(bool Success, string? Error);

public sealed record AudioStopResult(bool WasPlaying, bool Success);

public sealed record AudioJoinResult(bool Success, string? Error);

public interface IAudioPlayerService
{
    Task<AudioTrackResolveResult> ResolveAsync(string? origin, CancellationToken cancellationToken = default);

    Task<AudioPlayResult> PlayAsync(
        ulong guildId,
        ulong voiceChannelId,
        string identifier,
        CancellationToken cancellationToken = default);

    Task<AudioJoinResult> JoinAsync(
        ulong guildId,
        ulong voiceChannelId,
        CancellationToken cancellationToken = default);

    Task<AudioStopResult> LeaveAsync(ulong guildId, CancellationToken cancellationToken = default);

    Task<AudioStopResult> StopAsync(ulong guildId, CancellationToken cancellationToken = default);
}
