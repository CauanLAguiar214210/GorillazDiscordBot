using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class InflationService
{
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IEconomyRepository _economy;
    private ulong? _cachedSupply;
    private DateTime _cacheExpiresAt;

    public InflationService(IEconomyRepository economy)
    {
        _economy = economy;
    }

    public async Task<ulong> GetSupplyAsync()
    {
        if (_cachedSupply is { } supply && DateTime.UtcNow < _cacheExpiresAt)
            return supply;

        var fresh = await _economy.GetTotalMoneySupplyAsync();
        _cachedSupply = fresh;
        _cacheExpiresAt = DateTime.UtcNow.Add(CacheDuration);
        return fresh;
    }

    public async Task<double> GetIndexAsync()
        => InflationRules.Index(await GetSupplyAsync());
}