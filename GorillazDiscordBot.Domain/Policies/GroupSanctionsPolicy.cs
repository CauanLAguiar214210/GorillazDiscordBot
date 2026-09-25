using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Domain.Policies;

/// <summary>
/// Política padrão de sanção de contas vinculadas: se qualquer membro do grupo
/// estiver banido, o grupo inteiro é banido; caso contrário, se houver mute
/// ativo, o maior mute restante é aplicado ao membro que está entrando.
/// </summary>
public sealed class GroupSanctionsPolicy : IAltSanctionPolicy
{
    public AltSanctionDecision Decide(IReadOnlyList<AltMemberState> members)
    {
        if (members.Any(m => m.IsBanned))
            return new AltSanctionDecision(AltSanctionAction.Ban, null);

        DateTime? maxMuteUntil = null;
        foreach (var member in members)
        {
            if (member.MuteUntil is not { } until || until <= DateTime.UtcNow)
                continue;

            if (maxMuteUntil is null || until > maxMuteUntil)
                maxMuteUntil = until;
        }

        if (maxMuteUntil is not { } muteUntil)
            return AltSanctionDecision.None;

        var remaining = muteUntil - DateTime.UtcNow;
        return remaining > TimeSpan.Zero
            ? new AltSanctionDecision(AltSanctionAction.Timeout, remaining)
            : AltSanctionDecision.None;
    }
}