using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class CapitaoNavioGameTests
{
    [Fact]
    public void Build_GeraManobrasSegurasEConflitantes()
    {
        var rng = new Random(11);
        var safe = 0;
        var conflict = 0;

        for (var i = 0; i < 500; i++)
        {
            var maniobra = CapitaoNavioGame.Build(i % 8, rng);
            if (maniobra.HasConflict) conflict++;
            else safe++;
        }

        safe.Should().BeGreaterThan(0);
        conflict.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ToRound_ManobraSegura_AprovaSemBonus()
    {
        var round = CapitaoNavioGame.ToRound(new CapitaoNavioManiobra("Atracar no berço 3", "Berço livre", true));

        round.CorrectOption.Should().Be("\u2705 Aprovar");
        round.Bonus.Should().Be(0);
        round.Event.Should().BeNull();
        round.Options.Should().HaveCount(2);
        round.Options.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ToRound_Conflito_RejeitaCreditaBonus()
    {
        var round = CapitaoNavioGame.ToRound(new CapitaoNavioManiobra("Seguir rumo 040", "Pesca fechando à proa", false));

        round.CorrectOption.Should().Be("\u26D4 Rejeitar");
        round.Bonus.Should().Be(CapitaoNavioGame.ConflictBonus);
        round.Event.Should().Be("Conflito detectado");
    }

    [Fact]
    public void Step_SempreValido()
    {
        var rng = new Random(5);

        for (var i = 0; i < 200; i++)
        {
            var round = CapitaoNavioGame.Step(i % 8, rng);

            round.IsValid.Should().BeTrue();
            round.Options.Should().HaveCount(2);
            round.CorrectIndex.Should().BeInRange(0, 1);
            round.Prompt.Should().Contain("Tráfego Marítimo");
        }
    }
}