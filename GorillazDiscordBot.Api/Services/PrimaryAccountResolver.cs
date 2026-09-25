using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class PrimaryAccountResolver : IPrimaryAccountResolver
{
    private readonly IUserRepository _users;

    public PrimaryAccountResolver(IUserRepository users)
    {
        _users = users;
    }

    public async Task<ulong> ResolveMainIdAsync(ulong requestedUserId)
        => await _users.GetMainIdAsync(requestedUserId);
}