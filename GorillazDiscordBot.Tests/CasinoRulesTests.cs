using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class CasinoRulesTests
{
    [Fact]
    public void IsValidBet_AceitaValoresDentroDoIntervalo()
    {
        CasinoRules.IsValidBet(CasinoRules.MinBet).Should().BeTrue();
        CasinoRules.IsValidBet(CasinoRules.MaxBet).Should().BeTrue();
        CasinoRules.IsValidBet(500).Should().BeTrue();
    }

    [Fact]
    public void IsValidBet_RejeitaForaDoIntervalo()
    {
        CasinoRules.IsValidBet(0).Should().BeFalse();
        CasinoRules.IsValidBet(CasinoRules.MinBet - 1).Should().BeFalse();
        CasinoRules.IsValidBet(CasinoRules.MaxBet + 1).Should().BeFalse();
    }
}
