using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class LicencaExamSessionServiceTests
{
    private static LicencaExamSessionService CreateService()
        => new((pool, count) => pool.Take(count).ToList());

    [Fact]
    public void TryStart_SorteiaTresQuestoesDistintas()
    {
        var service = CreateService();

        service.TryStart(1, LicenseLevel.B, out var session).Should().BeTrue();

        session.Level.Should().Be(LicenseLevel.B);
        session.Questions.Should().HaveCount(3);
        session.Questions.Should().OnlyHaveUniqueItems();
        session.CurrentIndex.Should().Be(0);
        session.CorrectCount.Should().Be(0);
    }

    [Fact]
    public void TryStart_ComProvaEmAndamento_RejeitaERetornaAMesma()
    {
        var service = CreateService();

        service.TryStart(1, LicenseLevel.B, out var first).Should().BeTrue();
        service.TryStart(1, LicenseLevel.Arrais, out var second).Should().BeFalse();

        second.Should().BeSameAs(first);
        second.Level.Should().Be(LicenseLevel.B);
    }

    [Fact]
    public void Cancel_RemoveSessao()
    {
        var service = CreateService();
        service.TryStart(1, LicenseLevel.Capitao, out _);

        service.Cancel(1).Should().BeTrue();

        service.Get(1).Should().BeNull();
        service.Cancel(1).Should().BeFalse();
    }

    [Fact]
    public void Sessao_AprovacaoDependeDeDoisAcertos()
    {
        var service = CreateService();
        service.TryStart(1, LicenseLevel.B, out var session);

        session.CorrectCount = 1;
        session.Passed.Should().BeFalse();

        session.CorrectCount = LicenseQuizzes.PassingScore;
        session.Passed.Should().BeTrue();
    }
}