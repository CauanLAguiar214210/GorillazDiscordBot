namespace GorillazDiscordBot.Domain.Interfaces;

public enum AltSanctionAction
{
    None,
    Ban,
    Timeout,
}

public readonly record struct AltSanctionDecision(AltSanctionAction Action, TimeSpan? Timeout)
{
    public static AltSanctionDecision None { get; } = new(AltSanctionAction.None, null);
}

/// <summary>Estado observável de um membro do grupo vinculado para decisão de sanção.</summary>
public sealed record AltMemberState(bool IsBanned, DateTime? MuteUntil);

/// <summary>
/// Política de sanção para contas vinculadas (alt accounts): dado o estado dos
/// membros do grupo, decide a sanção a aplicar a quem tenta entrar no servidor.
/// A aplicação (ban/timeout) fica a cargo do chamador (sink de eventos Discord).
/// </summary>
public interface IAltSanctionPolicy
{
    AltSanctionDecision Decide(IReadOnlyList<AltMemberState> members);
}