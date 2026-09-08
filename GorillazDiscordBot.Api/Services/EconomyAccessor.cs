using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class EconomyAccessor : IEconomyAccessor
{
    private readonly IUserRepository _users;

    public EconomyAccessor(IUserRepository users)
    {
        _users = users;
    }

    public async Task<ulong> ResolveMainIdAsync(ulong requestedUserId)
        => await _users.GetMainIdAsync(requestedUserId);
}