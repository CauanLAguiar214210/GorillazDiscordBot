using FluentAssertions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Domain.Policies;

namespace GorillazDiscordBot.Tests;

public class GroupSanctionsPolicyTests
{
    private readonly GroupSanctionsPolicy _policy = new();

    private static AltMemberState Clean(DateTime? muteUntil = null)
        => new(false, muteUntil);

    private static AltMemberState Banned => new(true, null);

    [Fact]
    public void Decide_ListaVazia_RetornaNone()
    {
        _policy.Decide([]).Action.Should().Be(AltSanctionAction.None);
    }

    [Fact]
    public void Decide_SemMuteSemBanido_RetornaNone()
    {
        var decision = _policy.Decide([Clean(), Clean()]);

        decision.Action.Should().Be(AltSanctionAction.None);
    }

    [Fact]
    public void Decide_QualquerMembroBanido_RetornaBan()
    {
        var decision = _policy.Decide([Clean(), Banned, Clean()]);

        decision.Action.Should().Be(AltSanctionAction.Ban);
    }

    [Fact]
    public void Decide_MuteExpirado_RetornaNone()
    {
        var decision = _policy.Decide([Clean(muteUntil: DateTime.UtcNow.AddMinutes(-5))]);

        decision.Action.Should().Be(AltSanctionAction.None);
    }

    [Fact]
    public void Decide_MuteAtivo_RetornaTimeoutComMaiorMuteRestante()
    {
        var now = DateTime.UtcNow;
        var decision = _policy.Decide(
        [
            Clean(muteUntil: now.AddMinutes(5)),
            Clean(muteUntil: now.AddMinutes(30))
        ]);

        decision.Action.Should().Be(AltSanctionAction.Timeout);
        decision.Timeout.Should().NotBeNull();
        decision.Timeout!.Value.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5));
    }
}