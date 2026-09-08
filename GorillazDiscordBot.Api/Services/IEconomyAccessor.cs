using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public interface IEconomyAccessor
{
    Task<ulong> ResolveMainIdAsync(ulong requestedUserId);
}