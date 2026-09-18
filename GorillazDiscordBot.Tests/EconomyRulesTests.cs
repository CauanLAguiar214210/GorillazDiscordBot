using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class EconomyRulesTests
{
    [Fact]
    public void GetDailyReward_EstaDentroDoIntervalo()
    {
        var rng = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            var reward = EconomyRules.GetDailyReward(rng);
            reward.Should().BeGreaterThanOrEqualTo(EconomyRules.DailyMin);
            reward.Should().BeLessThanOrEqualTo(EconomyRules.DailyMax);
        }
    }

    [Fact]
    public void GetDailyInterestRate_EstaDentroDoIntervaloBase()
    {
        var rng = new Random(42);

        for (int i = 0; i < 1000; i++)
        {
            var rate = EconomyRules.GetDailyInterestRate(rng, streak: 0);
            rate.Should().BeGreaterThanOrEqualTo(EconomyRules.DailyInterestMin);
            rate.Should().BeLessThan(EconomyRules.DailyInterestMax + 1e-9);
        }
    }

    [Fact]
    public void GetDailyInterestRate_StreakAumentaTaxa()
    {
        var rng = new Random(42);
        var baseRate = EconomyRules.GetDailyInterestRate(new Random(7), 0);
        var boostedRate = EconomyRules.GetDailyInterestRate(new Random(7), 5);

        boostedRate.Should().BeGreaterThan(baseRate);
    }

    [Fact]
    public void ComputeInterestAmount_UsaPiso()
    {
        EconomyRules.ComputeInterestAmount(100, 0.005).Should().Be(0);
        EconomyRules.ComputeInterestAmount(1000, 0.035).Should().Be(35);
    }

    [Fact]
    public void ComputeRobAmount_AplicaVintePorcentoComTeto()
    {
        EconomyRules.ComputeRobAmount(500, new Random(1)).Should().Be(100);
        EconomyRules.ComputeRobAmount(5000, new Random(1)).Should().Be(EconomyRules.RobMaxSteal);
    }

    [Fact]
    public void ComputeRobAmount_NuncaRetornaZero()
    {
        EconomyRules.ComputeRobAmount(1, new Random(1)).Should().Be(1);
    }

    [Fact]
    public void ShouldRobSucceed_TaxaDeSucessoProximaDeQuarentaPorcento()
    {
        var rng = new Random(99);
        int wins = 0;
        const int attempts = 20000;

        for (int i = 0; i < attempts; i++)
            if (EconomyRules.ShouldRobSucceed(rng)) wins++;

        var rate = (double)wins / attempts;
        rate.Should().BeApproximately(EconomyRules.RobSuccessChance, 0.02);
    }

    [Fact]
    public void GetRemainingCooldown_CalculaCorretamente()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        EconomyRules.GetRemainingCooldown(null, now, EconomyRules.RobCooldown).Should().BeNull();
        EconomyRules.GetRemainingCooldown(now.AddMinutes(-2), now, TimeSpan.FromMinutes(5))
            .Should().Be(TimeSpan.FromMinutes(3));
        EconomyRules.GetRemainingCooldown(now.AddMinutes(-10), now, TimeSpan.FromMinutes(5)).Should().BeNull();
    }

    [Fact]
    public void WorkCooldown_FaculdadeUsaCincoMinutos()
    {
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("professor")!).Should().Be(TimeSpan.FromMinutes(5));
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("programador")!).Should().Be(TimeSpan.FromMinutes(5));
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("engenheiro")!).Should().Be(TimeSpan.FromMinutes(5));
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("motorista-app")!).Should().Be(TimeSpan.FromHours(3));
    }

    [Fact]
    public void WorkCooldown_JogosDeVeiculoUsamCincoMinutos()
    {
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("condutor-lancha")!).Should().Be(TimeSpan.FromMinutes(5));
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("comandante-iate")!).Should().Be(TimeSpan.FromMinutes(5));
        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("capitao-navio")!).Should().Be(TimeSpan.FromMinutes(5));

        EconomyRules.WorkCooldown(EconomyJobs.FindByKey("manobrista")!).Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void EconomyJobs_EncontraPorChaveOuNome()
    {
        EconomyJobs.Find("programador").Should().NotBeNull();
        EconomyJobs.Find("Programador").Should().NotBeNull();
        EconomyJobs.Find("inexistente").Should().BeNull();
    }

    [Fact]
    public void EconomyJobs_EncontraTrabalhosDeVeiculos()
    {
        EconomyJobs.Find("manobrista").Should().NotBeNull();
        EconomyJobs.Find("Manobrista").Should().NotBeNull();
        EconomyJobs.Find("motorista-app").Should().NotBeNull();
        EconomyJobs.Find("piloto-aviao").Should().NotBeNull();
        EconomyJobs.Find("piloto-comercial").Should().NotBeNull();
        EconomyJobs.Find("piloto-linha-aerea").Should().NotBeNull();
        EconomyJobs.Find("condutor-lancha").Should().NotBeNull();
        EconomyJobs.Find("comandante-iate").Should().NotBeNull();
        EconomyJobs.Find("capitao-navio").Should().NotBeNull();
    }

    [Fact]
    public void Veiculos_SaoTrabalhosLicenciadosSemDiploma()
    {
        foreach (var job in EconomyJobs.Veiculos)
        {
            job.Category.Should().Be(JobCategory.Veiculo);
            job.RequiresDiploma.Should().BeFalse();
            job.MinSchooling.Should().Be(SchoolingLevel.Nenhuma);
            job.RequiredLicense.Should().NotBeNull();
        }
    }

    [Fact]
    public void Pilotos_TemBonusParticularPorTipo()
    {
        EconomyJobs.FindByKey("piloto-aviao")!.TypeBonusPercent.Should().Be(5);
        EconomyJobs.FindByKey("piloto-comercial")!.TypeBonusPercent.Should().Be(5);
        EconomyJobs.FindByKey("piloto-linha-aerea")!.TypeBonusPercent.Should().Be(10);
        foreach (var job in EconomyJobs.Veiculos.Where(j => j.RequiredVehicleType is not null))
            job.CategoryBonusPercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Aquaticos_TemBonusParticularPorTipo()
    {
        EconomyJobs.FindByKey("condutor-lancha")!.TypeBonusPercent.Should().Be(5);
        EconomyJobs.FindByKey("comandante-iate")!.TypeBonusPercent.Should().Be(5);
        EconomyJobs.FindByKey("capitao-navio")!.TypeBonusPercent.Should().Be(10);

        EconomyJobs.FindByKey("condutor-lancha")!.RequiredLicense.Should().Be(LicenseLevel.Arrais);
        EconomyJobs.FindByKey("comandante-iate")!.RequiredLicense.Should().Be(LicenseLevel.Mestre);
        EconomyJobs.FindByKey("capitao-navio")!.RequiredLicense.Should().Be(LicenseLevel.Capitao);
        EconomyJobs.FindByKey("capitao-navio")!.VehicleDomain.Should().Be(LicenseDomain.Maritima);
    }
}