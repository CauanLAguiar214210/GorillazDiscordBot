using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class ManobristaSessionServiceTests
{
    private const ulong User = 1;

    private static ManobristaSessionService NewService(Func<double>? roll = null)
        => new(roll ?? (() => 1.0));

    [Fact]
    public void TryStart_CriaSessaoComSnapshot()
    {
        var service = NewService();
        service.TryStart(User, 250_000, out var session).Should().BeTrue();
        session.Should().NotBeNull();
        session.MoneySupply.Should().Be(250_000);
        session.Vagas.Should().Be(ManobristaRules.Vagas);
        session.BaseValue.Should().Be(ManobristaRules.BasePerCar);
        service.Get(User).Should().BeSameAs(session);
    }

    [Fact]
    public void TryStart_Duplicado_Recusado()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);
        service.TryStart(User, 0, out _).Should().BeFalse();
    }

    [Fact]
    public void TryStart_ComVagasEBasePersonalizados()
    {
        var service = NewService();
        service.TryStart(User, 0, 32, 14, out var session).Should().BeTrue();
        session.Vagas.Should().Be(32);
        session.BaseValue.Should().Be(14);
    }

    [Fact]
    public void TryPark_ContaAteVagasEEncerra()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);

        for (var i = 0; i < ManobristaRules.Vagas; i++)
            service.TryPark(User).Should().BeTrue();

        var session = service.Get(User)!;
        session.CarrosEstacionados.Should().Be(ManobristaRules.Vagas);
        session.IsFull.Should().BeTrue();
        session.Finished.Should().BeTrue();
        service.TryPark(User).Should().BeFalse();
    }

    [Fact]
    public void TryPark_ComVagasAmpliadas_PreencheAteVagas()
    {
        var service = NewService();
        const int vagas = 26;
        service.TryStart(User, 0, vagas, 10, out _);

        for (var i = 0; i < vagas; i++)
            service.TryPark(User).Should().BeTrue();

        var session = service.Get(User)!;
        session.CarrosEstacionados.Should().Be(vagas);
        session.IsFull.Should().BeTrue();
    }

    [Fact]
    public void TryPark_SemSessao_Recusado()
        => NewService().TryPark(999).Should().BeFalse();

    [Fact]
    public void Finish_EncerraSessao()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);
        service.TryPark(User);
        service.Finish(User);

        var session = service.Get(User)!;
        session.Finished.Should().BeTrue();
        service.TryPark(User).Should().BeFalse();
    }

    [Fact]
    public void TryStart_SessaoExpirada_Substitui()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);
        var old = service.Get(User)!;
        typeof(ManobristaSession).GetProperty(nameof(ManobristaSession.StartedAt))!
            .SetValue(old, DateTime.UtcNow.AddHours(-1));

        service.TryStart(User, 500_000, out var novo).Should().BeTrue();
        novo.MoneySupply.Should().Be(500_000);
    }

    [Fact]
    public void TryPark_ComboAcumulaDentroDaJanela()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);

        service.TryPark(User);
        service.TryPark(User);

        var session = service.Get(User)!;
        session.Combo.Should().Be(2);
        session.TotalRaw.Should().BeApproximately(10 * ManobristaRules.ComboMultiplier(1) + 10 * ManobristaRules.ComboMultiplier(2), 0.01);
        session.LastClickCoins.Should().BeApproximately(10 * ManobristaRules.ComboMultiplier(2), 0.01);
    }

    [Fact]
    public void TryPark_ComboZeraQuandoJanelaPassa()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);

        service.TryPark(User);
        service.TryPark(User);

        var session = service.Get(User)!;
        typeof(ManobristaSession).GetProperty(nameof(ManobristaSession.LastClickAt))!
            .SetValue(session, DateTime.UtcNow.AddSeconds(-10));

        service.TryPark(User);

        session.Combo.Should().Be(1);
        session.LastClickCoins.Should().BeApproximately(session.BaseValue * ManobristaRules.ComboMultiplier(1), 0.01);
    }

    [Fact]
    public void TryPark_EventoGorjeta_AdicionaRecompensa()
    {
        var service = NewService(() => 0.0);
        service.TryStart(User, 0, out _);

        service.TryPark(User);

        var session = service.Get(User)!;
        session.LastEvent.Should().Be(ManobristaEvent.Gorjeta);
        session.TotalRaw.Should().BeApproximately(10 + ManobristaRules.GorjetaReward, 0.01);
    }

    [Fact]
    public void TryPark_EventoVip_BonusMultiplicado()
    {
        var service = NewService(() => 0.20);
        service.TryStart(User, 0, out _);

        service.TryPark(User);

        var session = service.Get(User)!;
        session.LastEvent.Should().Be(ManobristaEvent.Vip);
        session.TotalRaw.Should().BeApproximately(10 + 10 * (ManobristaRules.VipMultiplier - 1), 0.01);
    }

    [Fact]
    public void TryPark_EventoRiscado_RemoveCarro()
    {
        var service = NewService(() => 0.24);
        service.TryStart(User, 0, out _);

        service.TryPark(User);
        service.TryPark(User);

        var session = service.Get(User)!;
        session.CarrosEstacionados.Should().Be(0);
        session.TotalRaw.Should().BeApproximately(10 * ManobristaRules.ComboMultiplier(1) + 10 * ManobristaRules.ComboMultiplier(2), 0.01);
        session.LastEvent.Should().Be(ManobristaEvent.Riscado);
    }

    [Fact]
    public void TryPark_SemEvento_CarroFica()
    {
        var service = NewService();
        service.TryStart(User, 0, out _);

        service.TryPark(User);
        service.TryPark(User);

        var session = service.Get(User)!;
        session.LastEvent.Should().Be(ManobristaEvent.Nenhum);
        session.CarrosEstacionados.Should().Be(2);
    }
}