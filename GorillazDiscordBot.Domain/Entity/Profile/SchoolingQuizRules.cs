namespace GorillazDiscordBot.Domain.Entity.Profile;

public sealed record MathQuestion(string Text, IReadOnlyList<string> Options, int CorrectIndex)
{
    public string CorrectOption => Options[CorrectIndex];
}

public static class SchoolingProgression
{
    public static IReadOnlyList<SchoolingLevel> Levels { get; } = new[]
    {
        SchoolingLevel.EnsinoFundamental1,
        SchoolingLevel.EnsinoFundamental2,
        SchoolingLevel.EnsinoMedio,
        SchoolingLevel.EnsinoSuperior
    };

    public static SchoolingLevel? Next(SchoolingLevel current)
    {
        var index = -1;
        for (var i = 0; i < Levels.Count; i++)
        {
            if (Levels[i] == current)
            {
                index = i;
                break;
            }
        }

        var next = index + 1;
        return next < Levels.Count ? Levels[next] : null;
    }

    public static bool IsConcluded(SchoolingLevel current)
        => current == Levels[^1];
}

public static class SchoolingQuizzes
{
    public const int QuestionsPerExam = 3;

    private static readonly IReadOnlyDictionary<SchoolingLevel, IReadOnlyList<MathQuestion>> Bank =
        new Dictionary<SchoolingLevel, IReadOnlyList<MathQuestion>>
        {
            [SchoolingLevel.EnsinoFundamental1] = new[]
            {
                Q("Quanto é 7 + 5?", 1, "11", "12", "13", "14"),
                Q("Quanto é 45 + 38?", 1, "73", "83", "82", "93"),
                Q("Qual é a metade de 90?", 1, "40", "45", "50", "35"),
                Q("Quanto é 100 ÷ 4?", 0, "25", "30", "40", "4"),
                Q("Quanto é 250 ÷ 5?", 1, "45", "50", "55", "40"),
                Q("Quanto é (−5) + 8?", 0, "3", "−3", "13", "−13"),
                Q("Quanto é 6 + 9?", 2, "13", "14", "15", "16"),
                Q("Quanto é 20 − 7?", 1, "12", "13", "14", "11"),
                Q("Quanto é 9 + 8?", 1, "16", "17", "18", "15"),
                Q("Qual é a metade de 50?", 1, "20", "25", "30", "15")
            },
            [SchoolingLevel.EnsinoFundamental2] = new[]
            {
                Q("Quanto é 9 × 3?", 2, "21", "24", "27", "30"),
                Q("Quanto é 8 × 7?", 2, "48", "54", "56", "64"),
                Q("Quanto é 12 × 12?", 2, "124", "132", "144", "154"),
                Q("Quanto é 15% de 200?", 1, "25", "30", "35", "40"),
                Q("Quanto é 2³?", 1, "6", "8", "9", "12"),
                Q("Quanto é 20% de 150?", 1, "25", "30", "35", "20"),
                Q("Quanto é 6 × 6?", 2, "30", "32", "36", "42"),
                Q("Quanto é 10% de 80?", 1, "6", "8", "10", "12"),
                Q("Quanto é 5²?", 3, "10", "15", "20", "25"),
                Q("Quanto é 50% de 60?", 1, "25", "30", "35", "20")
            },
            [SchoolingLevel.EnsinoMedio] = new[]
            {
                Q("Se x + 7 = 15, quanto vale x?", 3, "6", "7", "9", "8"),
                Q("Quanto é (−3) × (−4)?", 1, "−12", "12", "7", "−7"),
                Q("Se 3x = 21, quanto vale x?", 1, "6", "7", "8", "9"),
                Q("Se x ÷ 4 = 5, quanto vale x?", 2, "9", "1", "20", "25"),
                Q("Resolva 2x − 6 = 4.", 3, "3", "4", "6", "5"),
                Q("Resolva 5x + 3 = 18.", 1, "2", "3", "4", "5"),
                Q("Se x − 3 = 10, quanto vale x?", 1, "7", "13", "10", "14"),
                Q("Quanto é (−2) × 5?", 1, "10", "−10", "−7", "7"),
                Q("Se 2x = 18, quanto vale x?", 1, "8", "9", "10", "6"),
                Q("Quanto é 25% de 80?", 0, "20", "25", "15", "30")
            },
            [SchoolingLevel.EnsinoSuperior] = new[]
            {
                Q("Quanto é 3² + 4²?", 3, "7", "12", "49", "25"),
                Q("Qual o valor de log₂(8)?", 2, "2", "4", "3", "6"),
                Q("Qual o valor de √144?", 0, "12", "14", "16", "11"),
                Q("Quanto é 2⁵?", 2, "10", "25", "32", "16"),
                Q("Quanto é 7! ÷ 6!?", 1, "6", "7", "42", "1"),
                Q("Qual o valor de log₁₀(1000)?", 2, "2", "10", "3", "100"),
                Q("Qual o valor de √81?", 1, "8", "9", "7", "11"),
                Q("Quanto é 3³?", 1, "9", "27", "12", "18"),
                Q("Qual o valor de log₃(9)?", 0, "2", "3", "4", "9"),
                Q("Quanto é 5!?", 2, "25", "60", "120", "100")
            }
        };

    public static IReadOnlyList<MathQuestion> For(SchoolingLevel level)
        => Bank.TryGetValue(level, out var questions) ? questions : Array.Empty<MathQuestion>();

    private static MathQuestion Q(string text, int correctIndex, params string[] options)
        => new(text, options, correctIndex);
}