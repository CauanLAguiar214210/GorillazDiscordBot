using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class LicenseRulesTests
{
    public static IEnumerable<object[]> Levels =>
        LicenseProgression.All.Select(l => new object[] { l });

    [Fact]
    public void Escada_TemOnzeNiveisEmOrdem()
    {
        LicenseProgression.All.Should().HaveCount(11);
        LicenseProgression.All[0].Should().Be(LicenseLevel.A);
        LicenseProgression.All[^1].Should().Be(LicenseLevel.PilotoLinhaAerea);
    }

    [Fact]
    public void Sequence_AgrupaEMantemOrdemIntraGrupo()
    {
        LicenseProgression.Sequence(LicenseDomain.Terrestre)
            .Should().BeEquivalentTo(new[] { LicenseLevel.A, LicenseLevel.B, LicenseLevel.C, LicenseLevel.D, LicenseLevel.E },
                o => o.WithStrictOrdering());

        LicenseProgression.Sequence(LicenseDomain.Maritima)
            .Should().BeEquivalentTo(new[] { LicenseLevel.Arrais, LicenseLevel.Mestre, LicenseLevel.Capitao },
                o => o.WithStrictOrdering());

        LicenseProgression.Sequence(LicenseDomain.Aerea)
            .Should().BeEquivalentTo(new[] { LicenseLevel.PilotoPrivado, LicenseLevel.PilotoComercial, LicenseLevel.PilotoLinhaAerea },
                o => o.WithStrictOrdering());
    }

    [Fact]
    public void ExamCost_ValorFixoPorGrupo()
    {
        LicenseCosts.ExamCost(LicenseLevel.A).Should().Be(2000);
        LicenseCosts.ExamCost(LicenseLevel.E).Should().Be(2000);
        LicenseCosts.ExamCost(LicenseLevel.Arrais).Should().Be(25000);
        LicenseCosts.ExamCost(LicenseLevel.Capitao).Should().Be(25000);
        LicenseCosts.ExamCost(LicenseLevel.PilotoPrivado).Should().Be(35000);
        LicenseCosts.ExamCost(LicenseLevel.PilotoLinhaAerea).Should().Be(35000);
    }

    [Fact]
    public void Domains_TemTresDominios()
    {
        LicenseProgression.Domains.Should().BeEquivalentTo(
            new[] { LicenseDomain.Terrestre, LicenseDomain.Maritima, LicenseDomain.Aerea },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public void ByDomain_AgrupaCorretamente()
    {
        LicenseProgression.ByDomain(LicenseDomain.Terrestre).Should().HaveCount(5);
        LicenseProgression.ByDomain(LicenseDomain.Terrestre)
            .Should().OnlyContain(l => LicenseProgression.Info(l).Domain == LicenseDomain.Terrestre);

        LicenseProgression.ByDomain(LicenseDomain.Maritima)
            .Should().BeEquivalentTo(new[] { LicenseLevel.Arrais, LicenseLevel.Mestre, LicenseLevel.Capitao },
                o => o.WithStrictOrdering());

        LicenseProgression.ByDomain(LicenseDomain.Aerea)
            .Should().BeEquivalentTo(new[] { LicenseLevel.PilotoPrivado, LicenseLevel.PilotoComercial, LicenseLevel.PilotoLinhaAerea },
                o => o.WithStrictOrdering());
    }

    [Fact]
    public void Previous_SegueAOrdemDentroDoGrupo()
    {
        LicenseProgression.Previous(LicenseLevel.A).Should().BeNull();
        LicenseProgression.Previous(LicenseLevel.B).Should().Be(LicenseLevel.A);
        LicenseProgression.Previous(LicenseLevel.E).Should().Be(LicenseLevel.D);

        LicenseProgression.Previous(LicenseLevel.Arrais).Should().BeNull();
        LicenseProgression.Previous(LicenseLevel.Mestre).Should().Be(LicenseLevel.Arrais);
        LicenseProgression.Previous(LicenseLevel.Capitao).Should().Be(LicenseLevel.Mestre);

        LicenseProgression.Previous(LicenseLevel.PilotoPrivado).Should().BeNull();
        LicenseProgression.Previous(LicenseLevel.PilotoLinhaAerea).Should().Be(LicenseLevel.PilotoComercial);
    }

    [Fact]
    public void Prerequisite_SegueALicencaAnteriorDoGrupo()
    {
        LicenseProgression.Prerequisite(LicenseLevel.A).Should().BeNull();
        LicenseProgression.Prerequisite(LicenseLevel.B).Should().Be(LicenseLevel.A);
        LicenseProgression.Prerequisite(LicenseLevel.E).Should().Be(LicenseLevel.D);

        LicenseProgression.Prerequisite(LicenseLevel.Arrais).Should().BeNull();
        LicenseProgression.Prerequisite(LicenseLevel.Capitao).Should().Be(LicenseLevel.Mestre);

        LicenseProgression.Prerequisite(LicenseLevel.PilotoPrivado).Should().BeNull();
        LicenseProgression.Prerequisite(LicenseLevel.PilotoLinhaAerea).Should().Be(LicenseLevel.PilotoComercial);
    }

    [Fact]
    public void MeetsPrerequisite_ExigePredecessorDoGrupo()
    {
        LicenseProgression.MeetsPrerequisite(LicenseLevel.A, new List<LicenseLevel>()).Should().BeTrue();
        LicenseProgression.MeetsPrerequisite(LicenseLevel.Capitao, new List<LicenseLevel>()).Should().BeFalse();

        LicenseProgression.MeetsPrerequisite(LicenseLevel.Capitao, new List<LicenseLevel> { LicenseLevel.Arrais })
            .Should().BeFalse();
        LicenseProgression.MeetsPrerequisite(LicenseLevel.Capitao, new List<LicenseLevel> { LicenseLevel.Arrais, LicenseLevel.Mestre })
            .Should().BeTrue();

        LicenseProgression.MeetsPrerequisite(LicenseLevel.Mestre, new List<LicenseLevel> { LicenseLevel.A })
            .Should().BeFalse();
    }

    [Fact]
    public void Next_EIsMax_SeguemPorGrupo()
    {
        LicenseProgression.Next(LicenseLevel.A).Should().Be(LicenseLevel.B);
        LicenseProgression.Next(LicenseLevel.Arrais).Should().Be(LicenseLevel.Mestre);
        LicenseProgression.Next(LicenseLevel.Capitao).Should().BeNull();
        LicenseProgression.Next(LicenseLevel.PilotoLinhaAerea).Should().BeNull();
        LicenseProgression.IsMax(LicenseLevel.PilotoLinhaAerea).Should().BeTrue();
        LicenseProgression.IsMax(LicenseLevel.Arrais).Should().BeFalse();
    }

    [Fact]
    public void Escolaridade_CresceComADominio()
    {
        LicenseProgression.RequiredSchooling(LicenseLevel.A).Should().Be(SchoolingLevel.EnsinoFundamental1);
        LicenseProgression.RequiredSchooling(LicenseLevel.B).Should().Be(SchoolingLevel.EnsinoFundamental2);
        LicenseProgression.RequiredSchooling(LicenseLevel.Capitao).Should().Be(SchoolingLevel.EnsinoSuperior);
        LicenseProgression.RequiredSchooling(LicenseLevel.PilotoPrivado).Should().Be(SchoolingLevel.EnsinoSuperior);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void For_TodasQuestoesValidas(LicenseLevel level)
    {
        var questions = LicenseQuizzes.For(level);

        questions.Should().HaveCount(6);
        foreach (var q in questions)
        {
            q.Text.Should().NotBeNullOrWhiteSpace();
            q.Options.Should().HaveCount(4);
            q.CorrectIndex.Should().BeInRange(0, 3);
            q.CorrectOption.Should().Be(q.Options[q.CorrectIndex]);
            q.Options.Should().OnlyHaveUniqueItems();
        }
    }
}