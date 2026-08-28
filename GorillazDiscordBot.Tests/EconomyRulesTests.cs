using FluentAssertions;
using GorillazDiscordBot.Economy;

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
    public void EconomyJobs_EncontraPorChaveOuNome()
    {
        EconomyJobs.Find("programador").Should().NotBeNull();
        EconomyJobs.Find("Programador").Should().NotBeNull();
        EconomyJobs.Find("inexistente").Should().BeNull();
    }
}