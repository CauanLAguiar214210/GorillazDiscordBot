using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class ProfessorGameTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(20)]
    public void BuildExam_RespeitaQuantidade(int count)
    {
        var rounds = ProfessorGame.BuildExam(count, new Random(count));

        rounds.Should().HaveCount(count);
        foreach (var round in rounds)
        {
            round.IsValid.Should().BeTrue();
            round.Options.Should().HaveCount(2);
            round.Options.Should().OnlyHaveUniqueItems();
            round.CorrectIndex.Should().BeInRange(0, 1);
            round.Prompt.Should().Contain("Resposta do aluno");
        }
    }

    [Fact]
    public void BuildExam_ClampAplica()
    {
        ProfessorGame.BuildExam(0, new Random(1)).Should().HaveCount(ProfessorGame.MinQuestions);
        ProfessorGame.BuildExam(999, new Random(1)).Should().HaveCount(ProfessorGame.MaxQuestions);
    }

    [Fact]
    public void BuildExam_NaoRepeteQuestoes()
    {
        var rounds = ProfessorGame.BuildExam(ProfessorGame.MaxQuestions, new Random(5));
        rounds.Select(r => r.Prompt).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void BuildItem_RespostaCoerenteComGabarito()
    {
        var rng = new Random(21);
        var question = new MathQuestion("Quanto é 2 + 2?", new[] { "3", "4", "5", "6" }, 1);

        for (var i = 0; i < 200; i++)
        {
            var item = ProfessorGame.BuildItem(SchoolingLevel.EnsinoMedio, question, rng);
            item.StudentAnswer.Should().BeOneOf(question.Options);
            item.StudentIsCorrect.Should().Be(item.StudentAnswer == question.CorrectOption);
            item.IsCorrect.Should().Be(item.StudentIsCorrect);
        }
    }

    [Fact]
    public void BuildItem_ProduzAcertosEErros()
    {
        var rng = new Random(9);
        var question = new MathQuestion("Quanto é 2 + 2?", new[] { "3", "4", "5", "6" }, 1);

        var correct = 0;
        var wrong = 0;
        for (var i = 0; i < 500; i++)
        {
            var item = ProfessorGame.BuildItem(SchoolingLevel.EnsinoMedio, question, rng);
            if (item.StudentIsCorrect) correct++;
            else wrong++;
        }

        correct.Should().BeGreaterThan(0);
        wrong.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ToRound_IndiceEspelhaRespostaDoAluno()
    {
        var correct = new ProfessorExamItem("Médio", "2 + 2?", "4", true);
        var wrong = new ProfessorExamItem("Médio", "2 + 2?", "5", false);

        ProfessorGame.ToRound(correct).CorrectIndex.Should().Be(0);
        ProfessorGame.ToRound(wrong).CorrectIndex.Should().Be(1);
    }
}