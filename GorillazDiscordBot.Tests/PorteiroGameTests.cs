using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class PorteiroGameTests
{
    [Fact]
    public void Build_ArrivalOuIgualOuDivergente()
    {
        var rng = new Random(7);
        var legit = 0;
        var doppel = 0;

        for (var i = 0; i < 500; i++)
        {
            var check = PorteiroGame.Build(i % JobGameRules.RoundsPerSession, rng);

            check.IsDoppelganger.Should().Be(check.Arrival != check.Cadastro);

            if (check.IsDoppelganger)
            {
                doppel++;
                check.Arrival.Should().NotBe(check.Cadastro);
            }
            else
            {
                legit++;
                check.Arrival.Should().Be(check.Cadastro);
            }
        }

        legit.Should().BeGreaterThan(0);
        doppel.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ToRound_IndiceDerivadoDaComparacao()
    {
        var rng = new Random(11);

        for (var i = 0; i < 300; i++)
        {
            var check = PorteiroGame.Build(i % JobGameRules.RoundsPerSession, rng);
            var round = PorteiroGame.ToRound(check);

            round.CorrectIndex.Should().Be(check.IsMatch ? 0 : 1);
            round.CorrectOption.Should().Be(check.IsMatch ? "\u2705 Deixar entrar" : "\u26D4 Barrar");
            round.Prompt.Should().Contain("Cadastro").And.Contain("Chegou");
        }
    }

    [Fact]
    public void ToRound_DoppelgangerTemBonus()
    {
        var rng = new Random(3);

        for (var i = 0; i < 300; i++)
        {
            var check = PorteiroGame.Build(i % JobGameRules.RoundsPerSession, rng);
            var round = PorteiroGame.ToRound(check);

            if (check.IsDoppelganger)
            {
                round.Bonus.Should().Be(PorteiroGame.DoppelgangerBonus);
                round.Event.Should().Be("Impostor detectado");
            }
            else
            {
                round.Bonus.Should().Be(0);
                round.Event.Should().BeNull();
            }
        }
    }

    [Fact]
    public void MutationsFor_EscalaComRodada()
    {
        PorteiroGame.MutationsFor(0).Should().Contain(PorteiroMutation.Name);
        PorteiroGame.MutationsFor(1).Should().Contain(PorteiroMutation.Apartment);
        PorteiroGame.MutationsFor(5).Should().Equal(PorteiroMutation.BadgeDigit);
        PorteiroGame.MutationsFor(7).Should().Equal(PorteiroMutation.BadgeDigit);
    }

    [Fact]
    public void Step_GeraRodadasValidas()
    {
        var rng = new Random(99);

        for (var round = 0; round < JobGameRules.RoundsPerSession * 20; round++)
        {
            var result = PorteiroGame.Step(round % JobGameRules.RoundsPerSession, rng);

            result.IsValid.Should().BeTrue();
            result.Options.Count.Should().Be(2);
            result.Options.Should().OnlyHaveUniqueItems();
        }
    }
}
