namespace GorillazDiscordBot.Domain.Interfaces;

/// <summary>
/// Contrato de identidade do domínio de economia: resolve o ID principal de um
/// usuário (mainId), incluindo contas vinculadas (alt accounts).
/// Pertence ao lado do bot — serviços externos recebem o mainId via claim JWT.
/// </summary>
public interface IPrimaryAccountResolver
{
    Task<ulong> ResolveMainIdAsync(ulong requestedUserId);
}