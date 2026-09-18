using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class JobGameSessionServiceTests
{
    private const ulong User = 1;

    private static JobGameSessionService NewService(
        Func<double>? roll = null,
        Func<JobGameKind, int, int, Random, JobGameRound>? roundFactory = null)
        => new(roll ?? (() => 1.0), roundFactory);

    private static int WrongIndex(JobGameSession session)
        => (session.CurrentRound.CorrectIndex + 1) % session.CurrentRound.Options.Count;

    [Fact]
    public void TryStart_CriaSessaoDeRodadaUnica()
    {
        var service = NewService();

        service.TryStart(User, JobGameKind.Porteiro, out var session).Should().BeTrue();
        session.Kind.Should().Be(JobGameKind.Porteiro);
        session.Revealed.Should().BeTrue();
        session.Finished.Should().BeFalse();
        session.CurrentRound.IsValid.Should().BeTrue();
        service.Get(User).Should().BeSameAs(session);
    }

    [Fact]
    public void TryStart_CozinheiroComecaEscondido()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Cozinheiro, out var session).Should().BeTrue();
        session.IsMemory.Should().BeTrue();
        session.Revealed.Should().BeFalse();
    }

    [Fact]
    public void TryStart_Duplicado_Recusado()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Entregador, out _);
        service.TryStart(User, JobGameKind.Entregador, out _).Should().BeFalse();
    }

    [Fact]
    public void TryPlay_SemSessao_RetornaInvalid()
    {
        var service = NewService();
        service.TryPlay(User, 0, out _).Should().Be(JobGamePlayResult.Invalid);
    }

    [Fact]
    public void TryPlay_Acerto_AcumulaComboEValor()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Entregador, out var session);

        var result = service.TryPlay(User, session.CurrentRound.CorrectIndex, out var updated);

        result.Should().Be(JobGamePlayResult.RoundCleared);
        updated.CorrectRounds.Should().Be(1);
        updated.Combo.Should().Be(1);
        updated.Errors.Should().Be(0);
        updated.RoundIndex.Should().Be(1);
        updated.TotalRaw.Should().Be(JobGameCatalog.BasePerRound(JobGameKind.Entregador));
        updated.LastWasCorrect.Should().BeTrue();
    }

    [Fact]
    public void TryPlay_Erro_IncrementaErroEResetaCombo()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Entregador, out var session);

        service.TryPlay(User, session.CurrentRound.CorrectIndex, out _);
        var result = service.TryPlay(User, WrongIndex(session), out var updated);

        result.Should().Be(JobGamePlayResult.Error);
        updated.Errors.Should().Be(1);
        updated.Combo.Should().Be(0);
        updated.CorrectRounds.Should().Be(1);
        updated.LastWasCorrect.Should().BeFalse();
    }

    [Fact]
    public void TryPlay_TresErros_Finaliza()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Porteiro, out var session);

        service.TryPlay(User, WrongIndex(session), out _);
        service.TryPlay(User, WrongIndex(session), out _);
        var result = service.TryPlay(User, WrongIndex(session), out var updated);

        result.Should().Be(JobGamePlayResult.Finished);
        updated.Finished.Should().BeTrue();
        updated.Errors.Should().Be(JobGameRules.MaxErrors);
        updated.FinishedReason.Should().Contain("erros");
    }

    [Fact]
    public void TryPlay_AposFinalizar_RetornaFinished()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Faxineiro, out var session);

        service.TryPlay(User, WrongIndex(session), out _);
        service.TryPlay(User, WrongIndex(session), out _);
        service.TryPlay(User, WrongIndex(session), out _);

        service.TryPlay(User, 0, out _).Should().Be(JobGamePlayResult.Finished);
    }

    [Fact]
    public void Memory_ReadyRevelaRodada()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Cozinheiro, out var session);

        service.Ready(User, out var revealed).Should().Be(JobGamePlayResult.StepCorrect);
        revealed.Revealed.Should().BeTrue();
        revealed.CurrentRound.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Memory_RodadaCompleta_AvancaEVoltaAEsconder()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Cozinheiro, out var session);
        service.Ready(User, out session);

        JobGamePlayResult result = JobGamePlayResult.StepCorrect;
        var guard = 0;
        while (result != JobGamePlayResult.RoundCleared && guard++ < 20)
            result = service.TryPlay(User, session.CurrentRound.CorrectIndex, out session);

        result.Should().Be(JobGamePlayResult.RoundCleared);
        session.CorrectRounds.Should().Be(1);
        session.RoundIndex.Should().Be(1);
        session.Revealed.Should().BeFalse();
    }

    [Fact]
    public void Gorjeta_ComRollBaixo_AdicionaBonus()
    {
        var service = NewService(() => 0.0);
        service.TryStart(User, JobGameKind.Entregador, out var session);

        service.TryPlay(User, session.CurrentRound.CorrectIndex, out var updated);

        updated.LastEvent.Should().Be("Gorjeta");
        updated.LastBonus.Should().Be(JobGameRules.GorjetaReward);
        updated.TotalRaw.Should().Be(JobGameCatalog.BasePerRound(JobGameKind.Entregador) + JobGameRules.GorjetaReward);
    }

    [Fact]
    public void Vip_ComRollNoIntervalo_AdicionaBonus()
    {
        var service = NewService(() => JobGameRules.GorjetaChance + 0.01);
        service.TryStart(User, JobGameKind.Entregador, out var session);

        service.TryPlay(User, session.CurrentRound.CorrectIndex, out var updated);

        updated.LastEvent.Should().Be("Cliente VIP");
        updated.LastBonus.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Doppelganger_AcertoCreditaBonus()
    {
        var service = NewService(
            () => 1.0,
            (_, _, _, _) => new JobGameRound("🚪 Chegou", new[] { "entrar", "barrar" }, 1, "Impostor detectado", 40));

        service.TryStart(User, JobGameKind.Entregador, out _);
        service.TryPlay(User, 1, out var updated);

        updated.LastWasCorrect.Should().BeTrue();
        updated.LastEvent.Should().Be("Impostor detectado");
        updated.LastBonus.Should().Be(40);
        updated.TotalRaw.Should().Be(JobGameCatalog.BasePerRound(JobGameKind.Entregador) + 40);
    }

    [Fact]
    public void Doppelganger_BonusComGorjeta_CombinaEventos()
    {
        var service = NewService(
            () => 0.0,
            (_, _, _, _) => new JobGameRound("🚪 Chegou", new[] { "entrar", "barrar" }, 1, "Impostor detectado", 40));

        service.TryStart(User, JobGameKind.Entregador, out _);
        service.TryPlay(User, 1, out var updated);

        updated.LastEvent.Should().Be("Impostor detectado + Gorjeta");
        updated.LastBonus.Should().Be(40 + JobGameRules.GorjetaReward);
        updated.TotalRaw.Should().Be(JobGameCatalog.BasePerRound(JobGameKind.Entregador) + 40 + JobGameRules.GorjetaReward);
    }

    [Fact]
    public void Professor_ComQuantidadeEscolhida_PreparaProva()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Professor, out var session, 12).Should().BeTrue();

        session.TotalRounds.Should().Be(12);
        session.PreparedRounds.Should().HaveCount(12);
        session.Revealed.Should().BeTrue();
        service.Get(User).Should().BeSameAs(session);
    }

    [Fact]
    public void Professor_SemQuantidade_UsaMaximo()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Professor, out var session);

        session.TotalRounds.Should().Be(ProfessorGame.MaxQuestions);
        session.PreparedRounds.Should().HaveCount(ProfessorGame.MaxQuestions);
    }

    [Fact]
    public void Professor_QuantidadeAcimaDoLimite_EhAjustada()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Professor, out var session, 999);

        session.TotalRounds.Should().Be(ProfessorGame.MaxQuestions);
        session.PreparedRounds.Should().HaveCount(ProfessorGame.MaxQuestions);
    }

    [Fact]
    public void Professor_ProvaCompleta_FinalizaAposMesmasRodadas()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.Professor, out var session, 3);

        for (var i = 0; i < 3; i++)
        {
            var result = service.TryPlay(User, session.CurrentRound.CorrectIndex, out session);
            if (i < 2)
                result.Should().Be(JobGamePlayResult.RoundCleared);
            else
                result.Should().Be(JobGamePlayResult.Finished);
        }

        session.Finished.Should().BeTrue();
        session.CorrectRounds.Should().Be(3);
        session.Errors.Should().Be(0);
        session.FinishedReason.Should().Contain("concluíd");
    }

    [Fact]
    public void ComandanteIate_OrdemEmMultiPassos_FinalizaAposTotalRounds()
    {
        var service = NewService();
        service.TryStart(User, JobGameKind.ComandanteIate, out var session);

        session.StepsForRound.Should().Be(ComandanteIateGame.SequenceLength);
        session.Revealed.Should().BeTrue();

        var result = JobGamePlayResult.StepCorrect;
        var guard = 0;
        while (result != JobGamePlayResult.Finished && guard++ < 2000)
            result = service.TryPlay(User, session.CurrentRound.CorrectIndex, out session);

        result.Should().Be(JobGamePlayResult.Finished);
        session.CorrectRounds.Should().Be(session.TotalRounds);
        session.Errors.Should().Be(0);
        session.FinishedReason.Should().Contain("concluíd");
    }

    [Fact]
    public void CapitaoNavio_ComFatorDeConflito_CreditaBonusAoRejeitar()
    {
        var service = NewService(
            () => 1.0,
            (_, _, _, _) => new JobGameRound(
                "\U0001F6A2 conflito", new[] { "\u2705 Aprovar", "\u26D4 Rejeitar" },
                1, "Conflito detectado", 80));

        service.TryStart(User, JobGameKind.CapitaoNavio, out _);
        service.TryPlay(User, 1, out var updated);

        updated.LastWasCorrect.Should().BeTrue();
        updated.LastEvent.Should().Be("Conflito detectado");
        updated.LastBonus.Should().Be(80);
        updated.TotalRaw.Should().Be(JobGameCatalog.BasePerRound(JobGameKind.CapitaoNavio) + 80);
    }
}