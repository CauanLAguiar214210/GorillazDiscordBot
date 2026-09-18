using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class ComandanteIateGameTests
{
    [Fact]
    public void Step_DeterministicoPorRoundEStep()
    {
        var first = ComandanteIateGame.Step(2, 1, new Random(1));
        var second = ComandanteIateGame.Step(2, 1, new Random(999));

        first.Prompt.Should().Be(second.Prompt);
        first.Options.Should().Equal(second.Options);
        first.CorrectIndex.Should().Be(second.CorrectIndex);
    }

    [Fact]
    public void Step_ExigeCadaEtapaUmaVezNaOrdem()
    {
        for (var roundIndex = 0; roundIndex < 8; roundIndex++)
        {
            var required = new string[ComandanteIateGame.SequenceLength];

            for (var step = 0; step < ComandanteIateGame.SequenceLength; step++)
            {
                var round = ComandanteIateGame.Step(roundIndex, step, new Random(roundIndex));

                round.IsValid.Should().BeTrue();
                round.Options.Should().HaveCount(ComandanteIateGame.SequenceLength);
                round.Options.Should().OnlyHaveUniqueItems();
                required[step] = round.Options[round.CorrectIndex];
            }

            required.Should().OnlyHaveUniqueItems();
        }
    }
}