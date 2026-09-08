using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class EconomyUnifierTests
{
    [Fact]
    public void Unify_CombinaFundos_ComidaNaCarteiraBancoPoupanca()
    {
        var source = new EconomyProfile { Money = 100, Bank = 200, Savings = 300 };
        var target = new EconomyProfile { Money = 10, Bank = 20, Savings = 30 };

        var result = EconomyUnifier.Unify(source, target);

        target.Money.Should().Be(110);
        target.Bank.Should().Be(220);
        target.Savings.Should().Be(330);
        result.Should().Be(new UnifyResult(100, 200, 300));
    }

    [Fact]
    public void Unify_MantemMaiorStreakECooldownsMaisRecentes()
    {
        var older = new DateTime(2020, 1, 1);
        var newer = new DateTime(2021, 6, 15);

        var source = new EconomyProfile
        {
            SavingsStreak = 7,
            LastDailyClaim = newer,
            LastWorkTime = older,
            RobCaughtUntil = newer
        };
        var target = new EconomyProfile
        {
            SavingsStreak = 2,
            LastDailyClaim = older,
            LastWorkTime = newer,
            RobCaughtUntil = older
        };

        EconomyUnifier.Unify(source, target);

        target.SavingsStreak.Should().Be(7);
        target.LastDailyClaim.Should().Be(newer);
        target.LastWorkTime.Should().Be(newer);
        target.RobCaughtUntil.Should().Be(newer);
    }

    [Fact]
    public void Unify_EmPerfilVazio_TrazTudoDaOrigem()
    {
        var source = new EconomyProfile { Money = 50, Bank = 60, Savings = 70, SavingsStreak = 3 };
        var target = new EconomyProfile { UserId = 1 };

        var result = EconomyUnifier.Unify(source, target);

        target.Money.Should().Be(50);
        target.Bank.Should().Be(60);
        target.Savings.Should().Be(70);
        target.SavingsStreak.Should().Be(3);
        result.Should().Be(new UnifyResult(50, 60, 70));
    }

    [Fact]
    public void CheckedAdd_Estoura_SaturaEmMaxValue()
    {
        EconomyUnifier.CheckedAdd(ulong.MaxValue, 1).Should().Be(ulong.MaxValue);
    }
}