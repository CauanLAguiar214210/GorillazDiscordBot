using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class QuizSessionServiceTests
{
    private static QuizSessionService CreateService()
        => new((pool, count) => pool.Take(count).ToList());

    [Fact]
    public void TryStart_SorteiaTresQuestoesDistintas()
    {
        var service = CreateService();

        service.TryStart(1, SchoolingLevel.EnsinoMedio, out var session).Should().BeTrue();

        session.Level.Should().Be(SchoolingLevel.EnsinoMedio);
        session.Questions.Should().HaveCount(3);
        session.Questions.Should().OnlyHaveUniqueItems();
        session.CurrentIndex.Should().Be(0);
        session.CorrectCount.Should().Be(0);
    }

    [Fact]
    public void TryStart_ComProvaEmAndamento_RejeitaERetornaAMesma()
    {
        var service = CreateService();

        service.TryStart(1, SchoolingLevel.EnsinoMedio, out var first).Should().BeTrue();
        service.TryStart(1, SchoolingLevel.EnsinoSuperior, out var second).Should().BeFalse();

        second.Should().BeSameAs(first);
        second.Level.Should().Be(SchoolingLevel.EnsinoMedio);
    }

    [Fact]
    public void TryStart_UsuariosDiferentes_NaoInterferem()
    {
        var service = CreateService();

        service.TryStart(1, SchoolingLevel.EnsinoMedio, out _).Should().BeTrue();
        service.TryStart(2, SchoolingLevel.EnsinoMedio, out _).Should().BeTrue();
    }

    [Fact]
    public void Get_RetornaSessaoExistente()
    {
        var service = CreateService();
        service.TryStart(1, SchoolingLevel.EnsinoMedio, out var created);

        service.Get(1).Should().BeSameAs(created);
        service.Get(99).Should().BeNull();
    }

    [Fact]
    public void Cancel_RemoveSessao()
    {
        var service = CreateService();
        service.TryStart(1, SchoolingLevel.EnsinoMedio, out _);

        service.Cancel(1).Should().BeTrue();

        service.Get(1).Should().BeNull();
        service.Cancel(1).Should().BeFalse();
    }

    [Fact]
    public void Sessao_AprovacaoDependeDeDoisAcertos()
    {
        var service = CreateService();
        service.TryStart(1, SchoolingLevel.EnsinoMedio, out var session);

        session.CorrectCount = 1;
        session.Passed.Should().BeFalse();

        session.CorrectCount = QuizSessionService.PassingScore;
        session.Passed.Should().BeTrue();
    }

    [Fact]
    public void Sessao_FinalizaAposResponderTodas()
    {
        var service = CreateService();
        service.TryStart(1, SchoolingLevel.EnsinoMedio, out var session);

        session.IsFinished.Should().BeFalse();

        session.CurrentIndex = session.Questions.Count;
        session.IsFinished.Should().BeTrue();
    }
}