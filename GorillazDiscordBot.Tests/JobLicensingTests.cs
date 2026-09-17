using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class JobLicensingTests
{
    [Theory]
    [InlineData("programador", true)]
    [InlineData("engenheiro", true)]
    [InlineData("entregador", false)]
    [InlineData("faxineiro", false)]
    public void HasExam_ConformeProfissao(string jobKey, bool expected)
    {
        JobLicensing.HasExam(jobKey).Should().Be(expected);
    }

    [Theory]
    [InlineData("programador")]
    [InlineData("engenheiro")]
    public void For_TodasQuestoesValidas(string jobKey)
    {
        var questions = JobLicensing.For(jobKey);

        questions.Should().NotBeEmpty();
        foreach (var q in questions)
        {
            q.Text.Should().NotBeNullOrWhiteSpace();
            q.Options.Should().HaveCount(4);
            q.CorrectIndex.Should().BeInRange(0, 3);
            q.CorrectOption.Should().Be(q.Options[q.CorrectIndex]);
            q.Options.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void For_ProfissaoSemProva_RetornaVazio()
    {
        JobLicensing.For("entregador").Should().BeEmpty();
    }

    [Fact]
    public void Empregos_ExigemDiplomaEProva()
    {
        foreach (var job in EconomyJobs.Empregos)
        {
            job.RequiresDiploma.Should().BeTrue();
            JobLicensing.HasExam(job.Key).Should().BeTrue();
        }
    }

    [Fact]
    public void SubEmpregos_NaoExigemDiploma()
    {
        foreach (var job in EconomyJobs.SubEmpregos)
        {
            job.RequiresDiploma.Should().BeFalse();
            job.MinSchooling.Should().Be(Domain.Entity.Profile.SchoolingLevel.Nenhuma);
        }
    }
}