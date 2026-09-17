using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class ClasseEconomicaTests
{
    [Theory]
    [InlineData(0UL, "Miserável")]
    [InlineData(4_999UL, "Miserável")]
    [InlineData(5_000UL, "Quebrado")]
    [InlineData(9_999UL, "Quebrado")]
    [InlineData(10_000UL, "Proletariado")]
    [InlineData(100_000UL, "Emergente")]
    [InlineData(1_000_000UL, "Rico")]
    [InlineData(100_000_000UL, "Milionário")]
    [InlineData(1_000_000_000UL, "Magnata")]
    [InlineData(100_000_000_000UL, "Oligarca")]
    [InlineData(ulong.MaxValue, "Oligarca")]
    public void Find_RetornaClasseDoLimiar(ulong patrimonio, string esperado)
    {
        ClasseEconomica.Find(patrimonio).Title.Should().Be(esperado);
    }

    [Fact]
    public void Find_ZeradoRetornaMiseravel()
    {
        ClasseEconomica.Find(0).Should().Be(ClasseEconomica.All[0]);
    }

    [Fact]
    public void All_OrdenadoCrescente()
    {
        ClasseEconomica.All.Should().BeInAscendingOrder(c => c.MinPatrimonio);
    }
}