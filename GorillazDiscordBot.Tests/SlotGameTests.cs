using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Tests;

public class SlotGameTests
{
    [Fact]
    public void Carretéis_SaoTresESimbolosValidos()
    {
        var game = new SlotGame();

        game.Reels.Should().HaveCount(3);
        game.Reels.Should().OnlyContain(s => SlotGame.Symbols.Contains(s));
    }

    [Fact]
    public void Multiplier_EhConsistenteComOsCarretéis()
    {
        var game = new SlotGame();

        int expected = (game.Reels[0] == game.Reels[1] && game.Reels[1] == game.Reels[2]) ? 5
            : (game.Reels[0] == game.Reels[1] || game.Reels[1] == game.Reels[2] || game.Reels[0] == game.Reels[2]) ? 2
            : 0;

        game.Multiplier.Should().Be(expected);
        game.IsWin.Should().Be(expected > 0);
    }

    [Fact]
    public void Multiplier_SempreEntre0_2_5()
    {
        for (int i = 0; i < 200; i++)
        {
            var game = new SlotGame();
            game.Multiplier.Should().BeOneOf(0, 2, 5);
        }
    }
}
