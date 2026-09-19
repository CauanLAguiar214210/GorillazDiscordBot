using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class CrimeRulesTests
{
    [Fact]
    public void Cooldowns_EstaoDeAcordoComAsEspecificacoes()
    {
        CrimeRules.FurtoCooldown.Should().Be(TimeSpan.Zero);
        CrimeRules.CrimeCooldown.Should().Be(TimeSpan.FromMinutes(1));
        CrimeRules.PrisonLockout.Should().Be(TimeSpan.FromMinutes(1));
        EconomyRules.RobCooldown.Should().Be(TimeSpan.FromMinutes(1));
        EconomyRules.RobCaughtLockout.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void ComputeFurtoReward_DentroDoIntervaloBase()
    {
        var rng = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            var reward = CrimeRules.ComputeFurtoReward(rng);
            reward.Should().BeGreaterThanOrEqualTo(CrimeRules.MinFurtoReward);
            reward.Should().BeLessThanOrEqualTo(CrimeRules.MaxFurtoReward);
        }
    }

    [Fact]
    public void ComputeFurtoReward_ComBonusDeEquipamento()
    {
        var rng = new Random(10);
        var baseReward = CrimeRules.ComputeFurtoReward(new Random(10), 0);
        var boostedReward = CrimeRules.ComputeFurtoReward(new Random(10), 20);

        boostedReward.Should().BeGreaterThanOrEqualTo(baseReward);
    }

    [Fact]
    public void ShouldFurtoSucceed_SemBonus_AproximadoDeSetentaPorcento()
    {
        var rng = new Random(99);
        int wins = 0;
        const int attempts = 10000;

        for (int i = 0; i < attempts; i++)
            if (CrimeRules.ShouldFurtoSucceed(rng)) wins++;

        var rate = (double)wins / attempts;
        rate.Should().BeApproximately(CrimeRules.BaseFurtoSuccessChance, 0.03);
    }

    [Fact]
    public void ShouldFurtoSucceed_ComLuvas_AumentaChance()
    {
        var rng = new Random(99);
        int baseWins = 0;
        int boostedWins = 0;
        const int attempts = 10000;

        for (int i = 0; i < attempts; i++)
        {
            if (CrimeRules.ShouldFurtoSucceed(new Random(i), 0)) baseWins++;
            if (CrimeRules.ShouldFurtoSucceed(new Random(i), 20)) boostedWins++;
        }

        boostedWins.Should().BeGreaterThan(baseWins);
    }

    [Fact]
    public void ShouldRobSucceed_ArmaAumentaChanceEDefesaDiminui()
    {
        var normalChance = CrimeRules.ShouldRobSucceed(new Random(1), 0, 0);
        var armedThief = CrimeRules.ShouldRobSucceed(new Random(1), 30, 0);
        var armedVictim = CrimeRules.ShouldRobSucceed(new Random(1), 0, 30);

        armedThief.Should().BeTrue();
        armedVictim.Should().BeFalse();
    }

    [Fact]
    public void ComputeRobAmount_AplicaCapEBonus()
    {
        // 20% de 5000 = 1000, cap padrão 1000
        CrimeRules.ComputeRobAmount(5000, 1000).Should().Be(1000);

        // 20% de 50000 = 10000, cap de arma pesada 15000
        CrimeRules.ComputeRobAmount(50000, 15000).Should().Be(10000);

        // Com bônus de pet (ex: Macaco-Batedor +10%)
        CrimeRules.ComputeRobAmount(5000, 1000, 10).Should().Be(1100);
    }

    [Fact]
    public void ComputeFine_NuncaMenorQueMinimo()
    {
        CrimeRules.ComputeFine(0).Should().Be(CrimeRules.MinFine);
        CrimeRules.ComputeFine(1000).Should().Be(150); // 15% de 1000
    }

    [Fact]
    public void ComputeBail_CalculaProporcionalComBase()
    {
        CrimeRules.ComputeBail(0).Should().Be(CrimeRules.BaseBailAmount);
        CrimeRules.ComputeBail(100_000).Should().Be(CrimeRules.BaseBailAmount + 500);
    }
}
