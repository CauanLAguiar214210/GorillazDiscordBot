using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Sistema de inflação desativado. Retorna supply fixo 0 sem consultar o banco.
/// </summary>
public class InflationService
{
#pragma warning disable IDE0060
    public InflationService(IEconomyRepository economy) { }
#pragma warning restore IDE0060

    public Task<ulong> GetSupplyAsync() => Task.FromResult(0UL);

    public Task<double> GetIndexAsync() => Task.FromResult(1.0);
}
