using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class JobLicensingTests
{
    [Theory]
    [InlineData("programador", true)]
    [InlineData("engenheiro", true)]
    [InlineData("professor", true)]
    [InlineData("entregador", false)]
    [InlineData("faxineiro", false)]
    public void HasExam_ConformeProfissao(string jobKey, bool expected)
    {
        JobLicensing.HasExam(jobKey).Should().Be(expected);
    }

    [Theory]
    [InlineData("programador")]
    [InlineData("engenheiro")]
    [InlineData("professor")]
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

    [Fact]
    public void Veiculos_ExigemLicencaENaoDiploma()
    {
        EconomyJobs.Veiculos.Should().NotBeEmpty();

        foreach (var job in EconomyJobs.Veiculos)
        {
            job.RequiresDiploma.Should().BeFalse();
            job.RequiredLicense.Should().NotBeNull();
            JobLicensing.HasExam(job.Key).Should().BeFalse();
        }
    }

    [Fact]
    public void Veiculos_LicencasEspecificas()
    {
        EconomyJobs.FindByKey("manobrista")!.RequiredLicense.Should().Be(LicenseLevel.B);
        EconomyJobs.FindByKey("motorista-app")!.RequiredLicense.Should().Be(LicenseLevel.B);
        EconomyJobs.FindByKey("piloto-aviao")!.RequiredLicense.Should().Be(LicenseLevel.PilotoPrivado);
        EconomyJobs.FindByKey("piloto-comercial")!.RequiredLicense.Should().Be(LicenseLevel.PilotoComercial);
        EconomyJobs.FindByKey("piloto-linha-aerea")!.RequiredLicense.Should().Be(LicenseLevel.PilotoLinhaAerea);
        EconomyJobs.FindByKey("condutor-lancha")!.RequiredLicense.Should().Be(LicenseLevel.Arrais);
        EconomyJobs.FindByKey("comandante-iate")!.RequiredLicense.Should().Be(LicenseLevel.Mestre);
        EconomyJobs.FindByKey("capitao-navio")!.RequiredLicense.Should().Be(LicenseLevel.Capitao);
    }
}