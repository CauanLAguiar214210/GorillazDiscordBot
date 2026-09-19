using GorillazDiscordBot.Domain.Entity.Release;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IReleaseNoteRepository
{
    Task<List<ReleaseNote>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<List<ReleaseNote>> GetUnannouncedAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default);
    Task<ReleaseNote?> GetByVersionAsync(string version, CancellationToken cancellationToken = default);
    Task<ReleaseNote?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkAnnouncedAsync(string version, DateTime announcedAtUtc, CancellationToken cancellationToken = default);
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}
