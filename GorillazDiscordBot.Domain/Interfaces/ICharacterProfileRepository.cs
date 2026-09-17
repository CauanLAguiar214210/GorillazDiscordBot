using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface ICharacterProfileRepository
{
    Task<CharacterProfile> GetOrCreateAsync(ulong userId, string username);
    Task<CharacterProfile?> GetAsync(ulong userId);
    Task SaveAsync(CharacterProfile profile);
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}