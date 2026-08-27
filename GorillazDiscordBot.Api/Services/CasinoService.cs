using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public sealed record SimpleBetResult(bool Success, bool Won, int Amount, int Balance, string? Error);

/// <summary>
/// Orquestra toda a movimentação de moedas e o ciclo de apostas do cassino.
/// Mantém os comandos finos e as regras dos jogos testáveis.
/// </summary>
public sealed class CasinoService
{
    private readonly IUserRepository _userRepository;

    public CasinoService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public static (bool valid, string? error) ValidateAmount(int amount)
    {
        if (amount <= 0)
            return (false, "⚠️ O valor deve ser positivo.");

        return (true, null);
    }

    /// <summary>Desconta a aposta da carteira no início de um jogo. Retorna erro se saldo insuficiente.</summary>
    public async Task<(bool deducted, int balance, string? error)> ReserveAsync(ulong userId, int amount)
    {
        if (amount <= 0)
            return (false, 0, "⚠️ O valor deve ser positivo.");

        var (deducted, balance) = await _userRepository.TryDeductMoneyAsync(userId, amount);

        return deducted
            ? (true, balance, null)
            : (false, balance, "❌ Você não tem moedas suficientes na carteira.");
    }

    public Task CreditAsync(ulong userId, int amount) => _userRepository.AddMoneyAsync(userId, amount);

    /// <summary>Aposta simples 50/50 (cara ou coroa).</summary>
    public async Task<SimpleBetResult> PlaceSimpleBetAsync(ulong userId, int amount)
    {
        var (deducted, balance, error) = await ReserveAsync(userId, amount);

        if (!deducted)
            return new SimpleBetResult(false, false, amount, balance, error);

        bool won = Random.Shared.Next(2) == 0;
        int newBalance = balance;

        if (won)
        {
            await _userRepository.AddMoneyAsync(userId, amount * 2);
            newBalance = balance + amount * 2;
        }

        return new SimpleBetResult(true, won, amount, newBalance, null);
    }
}
