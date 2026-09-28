using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Domain.Interfaces;

/// <summary>
/// Contrato do serviço de loja do domínio (relíquias, veículos, equipamento e
/// inventário). Implementado hoje por <c>ShopService</c> no bot sobre MongoDB;
/// no futuro, pelo serviço Coinflux. A conversão dos módulos de comando para
/// consumir esta interface é acompanhada em ADR-0002.
/// </summary>
public interface IShopService
{
    Task<(ShopItem? relic, int value, bool cashback)> GetEquippedRelicAsync(ulong userId);

    Task<ulong> GetInventoryValueAsync(ulong userId);
}