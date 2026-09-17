using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class SchoolingQuizRulesTests
{
    public static IEnumerable<object[]> Levels =>
        SchoolingProgression.Levels.Select(l => new object[] { l });

    [Theory]
    [MemberData(nameof(Levels))]
    public void For_TemDezQuestoes(SchoolingLevel level)
    {
        SchoolingQuizzes.For(level).Should().HaveCount(10);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void For_TodasQuestoesValidas(SchoolingLevel level)
    {
        foreach (var q in SchoolingQuizzes.For(level))
        {
            q.Text.Should().NotBeNullOrWhiteSpace();
            q.Options.Should().HaveCount(4);
            q.CorrectIndex.Should().BeInRange(0, 3);
            q.CorrectOption.Should().Be(q.Options[q.CorrectIndex]);
            q.Options.Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void For_Nenhuma_RetornaVazio()
    {
        SchoolingQuizzes.For(SchoolingLevel.Nenhuma).Should().BeEmpty();
    }

    [Fact]
    public void Next_SegueAOrdem()
    {
        SchoolingProgression.Next(SchoolingLevel.Nenhuma).Should().Be(SchoolingLevel.EnsinoFundamental1);
        SchoolingProgression.Next(SchoolingLevel.EnsinoFundamental1).Should().Be(SchoolingLevel.EnsinoFundamental2);
        SchoolingProgression.Next(SchoolingLevel.EnsinoFundamental2).Should().Be(SchoolingLevel.EnsinoMedio);
        SchoolingProgression.Next(SchoolingLevel.EnsinoMedio).Should().Be(SchoolingLevel.EnsinoSuperior);
        SchoolingProgression.Next(SchoolingLevel.EnsinoSuperior).Should().BeNull();
    }

    [Fact]
    public void IsConcluded_SoNoSuperior()
    {
        SchoolingProgression.IsConcluded(SchoolingLevel.EnsinoSuperior).Should().BeTrue();
        SchoolingProgression.IsConcluded(SchoolingLevel.EnsinoMedio).Should().BeFalse();
        SchoolingProgression.IsConcluded(SchoolingLevel.Nenhuma).Should().BeFalse();
    }
}