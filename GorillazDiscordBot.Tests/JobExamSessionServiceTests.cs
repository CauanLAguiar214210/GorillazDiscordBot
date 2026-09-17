using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class JobExamSessionServiceTests
{
    private static JobExamSessionService CreateService()
        => new((pool, count) => pool.Take(count).ToList());

    [Fact]
    public void TryStart_SorteiaTresQuestoesDistintas()
    {
        var service = CreateService();

        service.TryStart(1, "programador", out var session).Should().BeTrue();

        session.JobKey.Should().Be("programador");
        session.Questions.Should().HaveCount(3);
        session.Questions.Should().OnlyHaveUniqueItems();
        session.CurrentIndex.Should().Be(0);
        session.CorrectCount.Should().Be(0);
    }

    [Fact]
    public void TryStart_ComProvaEmAndamento_RejeitaERetornaAMesma()
    {
        var service = CreateService();

        service.TryStart(1, "programador", out var first).Should().BeTrue();
        service.TryStart(1, "engenheiro", out var second).Should().BeFalse();

        second.Should().BeSameAs(first);
        second.JobKey.Should().Be("programador");
    }

    [Fact]
    public void Cancel_RemoveSessao()
    {
        var service = CreateService();
        service.TryStart(1, "engenheiro", out _);

        service.Cancel(1).Should().BeTrue();

        service.Get(1).Should().BeNull();
        service.Cancel(1).Should().BeFalse();
    }

    [Fact]
    public void Sessao_AprovacaoDependeDeDoisAcertos()
    {
        var service = CreateService();
        service.TryStart(1, "programador", out var session);

        session.CorrectCount = 1;
        session.Passed.Should().BeFalse();

        session.CorrectCount = JobLicensing.PassingScore;
        session.Passed.Should().BeTrue();
    }
}