using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Domain.Entity.Economy;

public sealed record ProfessorExamItem(
    string Level,
    string Question,
    string StudentAnswer,
    bool StudentIsCorrect)
{
    public bool IsCorrect => StudentIsCorrect;
}

public static class ProfessorGame
{
    public const ulong BasePerRound = 120;
    public const int MinQuestions = 1;
    public const int MaxQuestions = 20;
    public const double StudentCorrectChance = 0.5;

    private static readonly string[] Decisions = { "\u2705 Correta", "\u274C Incorreta" };

    private static readonly (SchoolingLevel Level, MathQuestion Question)[] Pool = BuildPool();

    public static int ClampCount(int count) => Math.Clamp(count, MinQuestions, MaxQuestions);

    public static IReadOnlyList<JobGameRound> BuildExam(int count, Random rng)
    {
        var total = ClampCount(count);
        var picked = Pool.OrderBy(_ => rng.Next()).Take(total);

        var rounds = new List<JobGameRound>(total);
        foreach (var (level, question) in picked)
            rounds.Add(ToRound(BuildItem(level, question, rng)));

        return rounds;
    }

    public static JobGameRound Step(Random rng)
    {
        var (level, question) = Pool[rng.Next(Pool.Length)];
        return ToRound(BuildItem(level, question, rng));
    }

    public static ProfessorExamItem BuildItem(SchoolingLevel level, MathQuestion question, Random rng)
    {
        var studentIsCorrect = rng.NextDouble() < StudentCorrectChance;
        var answer = studentIsCorrect ? question.CorrectOption : WrongOption(question, rng);

        return new ProfessorExamItem(FormatLevel(level), question.Text, answer, studentIsCorrect);
    }

    public static JobGameRound ToRound(ProfessorExamItem item)
    {
        var prompt =
            $"\U0001F4DD **Correção** \u2014 Matemática ({item.Level})\n" +
            $"Questão: **{item.Question}**\n" +
            $"Resposta do aluno: **{item.StudentAnswer}**\n\n" +
            "O aluno acertou?";

        return new JobGameRound(prompt, Decisions, item.StudentIsCorrect ? 0 : 1);
    }

    private static string WrongOption(MathQuestion question, Random rng)
    {
        var wrong = question.Options.Where(o => o != question.CorrectOption).ToArray();
        return wrong[rng.Next(wrong.Length)];
    }

    private static (SchoolingLevel Level, MathQuestion Question)[] BuildPool()
    {
        var levels = new[]
        {
            SchoolingLevel.EnsinoFundamental1,
            SchoolingLevel.EnsinoFundamental2,
            SchoolingLevel.EnsinoMedio
        };

        return levels
            .SelectMany(level => SchoolingQuizzes.For(level).Select(q => (level, q)))
            .ToArray();
    }

    private static string FormatLevel(SchoolingLevel level) => level switch
    {
        SchoolingLevel.EnsinoFundamental1 => "Fundamental I",
        SchoolingLevel.EnsinoFundamental2 => "Fundamental II",
        SchoolingLevel.EnsinoMedio => "Médio",
        _ => "Básico"
    };
}
