using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class SlotMachineGameTests
{
    private static IReadOnlyList<SlotSymbol> Rows(params SlotSymbol[] symbols)
        => new List<SlotSymbol>(symbols);

    [Fact]
    public void Spin_RetornaTresSimbolos()
    {
        var game = new SlotMachineGame(() => SlotSymbol.Cherry);

        var reels = game.Spin();

        reels.Should().HaveCount(3);
        game.HasSpun.Should().BeTrue();
    }

    [Fact]
    public void Spin_DuasVezes_LancaExcecao()
    {
        var game = new SlotMachineGame(() => SlotSymbol.Cherry);
        game.Spin();

        var act = () => game.Spin();

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(SlotSymbol.Cherry, 2)]
    [InlineData(SlotSymbol.Lemon, 3)]
    [InlineData(SlotSymbol.Bell, 5)]
    [InlineData(SlotSymbol.Star, 10)]
    [InlineData(SlotSymbol.Seven, 20)]
    [InlineData(SlotSymbol.Diamond, 50)]
    public void TresIguais_MultiplicadorDaCadaSimbolo(SlotSymbol symbol, int multiplier)
    {
        var reels = Rows(symbol, symbol, symbol);

        SlotMachineGame.CalculateReturn(100, reels).Should().Be(100 * multiplier);
    }

    [Theory]
    [InlineData(SlotSymbol.Cherry, 1)]
    [InlineData(SlotSymbol.Lemon, 2)]
    [InlineData(SlotSymbol.Bell, 3)]
    [InlineData(SlotSymbol.Star, 5)]
    [InlineData(SlotSymbol.Seven, 8)]
    [InlineData(SlotSymbol.Diamond, 15)]
    public void ParDeSimbolos_MultiplicadorDePar(SlotSymbol symbol, int multiplier)
    {
        var other = symbol == SlotSymbol.Diamond ? SlotSymbol.Cherry : SlotSymbol.Diamond;
        var reels = Rows(symbol, symbol, other);

        SlotMachineGame.CalculateReturn(100, reels).Should().Be(100 * multiplier);
    }

    [Fact]
    public void ParNaoConsecutivo_TambemPaga()
    {
        var reels = Rows(SlotSymbol.Cherry, SlotSymbol.Lemon, SlotSymbol.Cherry);

        SlotMachineGame.CalculateReturn(100, reels).Should().Be(100);
    }

    [Fact]
    public void TodosDiferentes_Perde()
    {
        var reels = Rows(SlotSymbol.Cherry, SlotSymbol.Lemon, SlotSymbol.Bell);

        SlotMachineGame.CalculateReturn(100, reels).Should().Be(0);
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var reels = Rows(SlotSymbol.Cherry, SlotSymbol.Cherry, SlotSymbol.Cherry);

        var act = () => SlotMachineGame.CalculateReturn(0, reels);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MenosDeTresSimbolos_NaoPaga()
    {
        var reels = Rows(SlotSymbol.Cherry, SlotSymbol.Cherry);

        SlotMachineGame.CalculateReturn(100, reels).Should().Be(0);
    }

    [Fact]
    public void Multiplicador_MaisAltoParaDiamante()
    {
        SlotMachineGame.Multiplier(SlotSymbol.Diamond).Should()
            .BeGreaterThan(SlotMachineGame.Multiplier(SlotSymbol.Seven));
    }
}
