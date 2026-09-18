using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class JobGameRulesTests
{
    private static readonly JobGameKind[] AllKinds =
    {
        JobGameKind.Cozinheiro,
        JobGameKind.Porteiro,
        JobGameKind.Entregador,
        JobGameKind.Faxineiro,
        JobGameKind.Professor,
        JobGameKind.CondutorLancha,
        JobGameKind.ComandanteIate,
        JobGameKind.CapitaoNavio
    };

    [Fact]
    public void ComboMultiplier_ProgressaoLimpa()
    {
        JobGameRules.ComboMultiplier(1).Should().Be(1.0);
        JobGameRules.ComboMultiplier(2).Should().BeApproximately(1.0 + JobGameRules.ComboStep, 0.0001);
        JobGameRules.ComboMultiplier(999).Should().BeApproximately(1.0 + JobGameRules.ComboMaxBonus, 0.0001);
    }

    [Fact]
    public void Catalog_MapeiaSubempregos()
    {
        JobGameCatalog.ForJobKey("cozinheiro").Should().Be(JobGameKind.Cozinheiro);
        JobGameCatalog.ForJobKey("porteiro").Should().Be(JobGameKind.Porteiro);
        JobGameCatalog.ForJobKey("entregador").Should().Be(JobGameKind.Entregador);
        JobGameCatalog.ForJobKey("faxineiro").Should().Be(JobGameKind.Faxineiro);
        JobGameCatalog.ForJobKey("programador").Should().BeNull();
    }

    [Fact]
    public void Catalog_MapeiaProfessorComRoundsPreparaveis()
    {
        JobGameCatalog.ForJobKey("professor").Should().Be(JobGameKind.Professor);
        JobGameCatalog.RoundsFor(JobGameKind.Professor).Should().Be(ProfessorGame.MaxQuestions);
        JobGameCatalog.RoundsFor(JobGameKind.Porteiro).Should().Be(JobGameRules.RoundsPerSession);

        JobGameCatalog.ClampRounds(JobGameKind.Professor, 0).Should().Be(ProfessorGame.MinQuestions);
        JobGameCatalog.ClampRounds(JobGameKind.Professor, 999).Should().Be(ProfessorGame.MaxQuestions);
        JobGameCatalog.ClampRounds(JobGameKind.Porteiro, 3).Should().Be(3);

        var prepared = JobGameCatalog.PrepareRounds(JobGameKind.Professor, 7, new Random(1));
        prepared.Should().HaveCount(7);

        JobGameCatalog.PrepareRounds(JobGameKind.Porteiro, 7, new Random(1)).Should().BeNull();
        JobGameCatalog.Meta(JobGameKind.Professor).Title.Should().Be("Correção de Provas");
    }

    [Fact]
    public void Catalog_MapeiaVeiculosAquaticos()
    {
        JobGameCatalog.ForJobKey("condutor-lancha").Should().Be(JobGameKind.CondutorLancha);
        JobGameCatalog.ForJobKey("comandante-iate").Should().Be(JobGameKind.ComandanteIate);
        JobGameCatalog.ForJobKey("capitao-navio").Should().Be(JobGameKind.CapitaoNavio);
        JobGameCatalog.ForJobKey("manobrista").Should().BeNull();

        JobGameCatalog.BasePerRound(JobGameKind.CondutorLancha).Should().Be(150);
        JobGameCatalog.BasePerRound(JobGameKind.ComandanteIate).Should().Be(230);
        JobGameCatalog.BasePerRound(JobGameKind.CapitaoNavio).Should().Be(310);

        JobGameCatalog.StepsForRound(JobGameKind.ComandanteIate, 0).Should().Be(ComandanteIateGame.SequenceLength);
        JobGameCatalog.StepsForRound(JobGameKind.CondutorLancha, 3).Should().Be(1);
        JobGameCatalog.StepsForRound(JobGameKind.CapitaoNavio, 3).Should().Be(1);
        JobGameCatalog.RoundsFor(JobGameKind.CapitaoNavio).Should().Be(JobGameRules.RoundsPerSession);

        JobGameCatalog.PrepareRounds(JobGameKind.CapitaoNavio, 8, new Random(1)).Should().BeNull();
    }

    [Fact]
    public void BasePerRound_MaiorQueZeroParaTodosOsJogos()
    {
        foreach (var kind in AllKinds)
            JobGameCatalog.BasePerRound(kind).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Round_TodasAsKinds_TemOpcoesUnicasEIndiceValido()
    {
        var rng = new Random(42);

        foreach (var kind in AllKinds)
        {
            for (var round = 0; round < JobGameRules.RoundsPerSession; round++)
            {
                var steps = JobGameCatalog.StepsForRound(kind, round);
                for (var step = 0; step < steps; step++)
                {
                    var gameRound = JobGameCatalog.Round(kind, round, step, rng);

                    gameRound.IsValid.Should().BeTrue();
                    gameRound.Options.Should().NotBeEmpty();
                    gameRound.Options.Should().OnlyHaveUniqueItems();
                    gameRound.CorrectIndex.Should().BeInRange(0, gameRound.Options.Count - 1);
                    gameRound.CorrectOption.Should().Be(gameRound.Options[gameRound.CorrectIndex]);
                    gameRound.Prompt.Should().NotBeNullOrWhiteSpace();
                }
            }
        }
    }

    [Fact]
    public void Cozinheiro_StepsCrescemECapNoMaximo()
    {
        JobGameCatalog.IsMemory(JobGameKind.Cozinheiro).Should().BeTrue();
        CozinheiroGame.StepsForRound(0).Should().Be(CozinheiroGame.StartLength);

        for (var round = 0; round < JobGameRules.RoundsPerSession; round++)
            CozinheiroGame.StepsForRound(round).Should().BeLessThanOrEqualTo(CozinheiroGame.MaxLength);

        CozinheiroGame.StepsForRound(1).Should().Be(CozinheiroGame.StartLength + 1);
        CozinheiroGame.Reveal(0).Should().Contain("Memorize");
    }

    [Fact]
    public void JogosDeRodadaUnica_TemUmPasso()
    {
        JobGameCatalog.StepsForRound(JobGameKind.Porteiro, 5).Should().Be(1);
        JobGameCatalog.StepsForRound(JobGameKind.Entregador, 5).Should().Be(1);
        JobGameCatalog.StepsForRound(JobGameKind.Faxineiro, 5).Should().Be(1);
        JobGameCatalog.Reveal(JobGameKind.Porteiro, 0).Should().BeNull();
    }
}