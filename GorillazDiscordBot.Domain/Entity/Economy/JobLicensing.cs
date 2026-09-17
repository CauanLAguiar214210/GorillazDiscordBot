using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class JobLicensing
{
    public const int QuestionsPerExam = 3;
    public const int PassingScore = 2;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<MathQuestion>> Bank =
        new Dictionary<string, IReadOnlyList<MathQuestion>>
        {
            ["programador"] = new[]
            {
                Q("Na lógica booleana, se A = 1 e B = 0, quanto vale A E B?", 0, "0", "1", "2", "A"),
                Q("Qual estrutura segue a ordem 'primeiro a entrar, primeiro a sair'?", 1, "Pilha", "Fila", "Árvore", "Grafo"),
                Q("Quantos bytes tem 1 kilobyte (convenção comum)?", 3, "10", "100", "512", "1024"),
                Q("Qual número binário representa o decimal 5?", 1, "100", "101", "110", "111"),
                Q("Se um laço roda de 1 até 10, quantas iterações ele executa?", 2, "9", "11", "10", "100"),
                Q("Quanto é 16 ÷ 2 + 3?", 2, "5", "9", "11", "13")
            },
            ["engenheiro"] = new[]
            {
                Q("Quanto é 5² + 12?", 2, "25", "30", "37", "40"),
                Q("Um triângulo retângulo tem catetos 3 e 4. Qual é a hipotenusa?", 1, "6", "5", "8", "7"),
                Q("Quanto é 15% de 200?", 1, "20", "30", "35", "45"),
                Q("Se 3x = 27, quanto vale x?", 3, "6", "7", "12", "9"),
                Q("Qual é a área de um quadrado de lado 6?", 2, "12", "30", "36", "24"),
                Q("Quanto é 45 ÷ 9?", 0, "5", "6", "9", "4")
            }
        };

    public static bool HasExam(string jobKey)
        => Bank.ContainsKey(jobKey);

    public static IReadOnlyList<MathQuestion> For(string jobKey)
        => Bank.TryGetValue(jobKey, out var questions) ? questions : Array.Empty<MathQuestion>();

    private static MathQuestion Q(string text, int correctIndex, params string[] options)
        => new(text, options, correctIndex);
}