using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class CondutorLanchaGameTests
{
    [Fact]
    public void Step_SempreValidoComOpcoesUnicas()
    {
        var rng = new Random(42);

        for (var i = 0; i < 200; i++)
        {
            var round = CondutorLanchaGame.Step(rng);

            round.IsValid.Should().BeTrue();
            round.Options.Should().HaveCount(4);
            round.Options.Should().OnlyHaveUniqueItems();
            round.CorrectOption.Should().Be(round.Options[round.CorrectIndex]);
            round.Prompt.Should().Contain("Navegação Costeira");
        }
    }
}