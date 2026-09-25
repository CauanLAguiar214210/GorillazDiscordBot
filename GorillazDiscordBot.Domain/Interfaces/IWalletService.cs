using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Domain.Interfaces;

public sealed record AppliedRelic(string Name, string Emoji, RelicEffect Effect, int Value);

public sealed record PayoutResult(
    ulong Balance,
    ulong BaseReturn,
    ulong Bonus,
    AppliedRelic? Relic);

/// <summary>
/// Contrato do serviço de carteira — dono do dinheiro do jogador no domínio:
/// débito de aposta, crédito de retorno, saldo e aplicação de relíquias.
/// Hoje implementado por <c>PayoutService</c> no bot; no futuro, pelo serviço Coinflux.
/// </summary>
public interface IWalletService
{
    Task<ulong> GetBalanceAsync(ulong userId, string username);

    Task<(bool success, ulong balance)> DeductBetAsync(
        ulong userId, ulong amount, string username, string description);

    Task<ulong> RefundAsync(ulong userId, ulong amount, string username, string description);

    Task<PayoutResult> PayOutAsync(
        ulong userId, ulong returnAmount, string username, string description,
        RelicGameType gameType = RelicGameType.All, ulong totalBet = 0);
}