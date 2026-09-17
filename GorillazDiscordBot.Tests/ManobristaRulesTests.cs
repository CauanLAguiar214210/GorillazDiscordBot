using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class ManobristaRulesTests
{
    [Fact]
    public void ComboMultiplier_Combo1_EhBase()
        => ManobristaRules.ComboMultiplier(1).Should().Be(1.0);

    [Fact]
    public void ComboMultiplier_CrescePorClique()
    {
        ManobristaRules.ComboMultiplier(2).Should().BeApproximately(1.0 + ManobristaRules.ComboStep, 0.0001);
        ManobristaRules.ComboMultiplier(3).Should().BeApproximately(1.0 + 2 * ManobristaRules.ComboStep, 0.0001);
    }

    [Fact]
    public void ComboMultiplier_RespeitaTeto()
    {
        ManobristaRules.ComboMultiplier(999).Should().BeApproximately(1.0 + ManobristaRules.ComboMaxBonus, 0.0001);
    }

    [Fact]
    public void ComboMultiplier_ComboMenorQue1_RetornaBase()
        => ManobristaRules.ComboMultiplier(0).Should().Be(1.0);
}