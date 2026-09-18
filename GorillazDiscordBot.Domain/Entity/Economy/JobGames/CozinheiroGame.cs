namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class CozinheiroGame
{
    public const ulong BasePerRound = 60;
    public const int StartLength = 3;
    public const int MaxLength = 6;

    private static readonly string[] Pool =
    {
        "\U0001F95A", "\U0001F345", "\U0001F9C0", "\U0001F953", "\U0001F945",
        "\U0001F954", "\U0001F955", "\U0001F336\uFE0F", "\U0001F944", "\U0001F344"
    };

    private static readonly (string Dish, string[] Seq)[] Recipes =
    {
        ("Omelete Especial", new[] { "\U0001F95A", "\U0001F9C0", "\U0001F945", "\U0001F953", "\U0001F336\uFE0F", "\U0001F344" }),
        ("Molho Secreto", new[] { "\U0001F345", "\U0001F944", "\U0001F336\uFE0F", "\U0001F955", "\U0001F945", "\U0001F344" }),
        ("Batata Recheada", new[] { "\U0001F954", "\U0001F9C0", "\U0001F953", "\U0001F955", "\U0001F944", "\U0001F95A" }),
    };

    public static int StepsForRound(int roundIndex)
        => Math.Min(StartLength + roundIndex, MaxLength);

    public static string Reveal(int roundIndex)
    {
        var (dish, seq) = RecipeFor(roundIndex);
        return $"🧠 Memorize a sequência de **{dish}**:\n{string.Join("  ", seq)}";
    }

    public static JobGameRound Step(int roundIndex, int stepIndex, Random rng)
    {
        var (dish, seq) = RecipeFor(roundIndex);
        var answer = seq[stepIndex];
        var options = Pool
            .Where(p => p != answer)
            .OrderBy(_ => rng.Next())
            .Take(3)
            .Append(answer)
            .OrderBy(_ => rng.Next())
            .ToList();

        return new JobGameRound(
            $"🍳 **{dish}** — Qual é o {Ordinal(stepIndex + 1)}º ingrediente?",
            options,
            options.IndexOf(answer));
    }

    private static (string Dish, string[] Seq) RecipeFor(int roundIndex)
    {
        var recipe = Recipes[roundIndex % Recipes.Length];
        var len = StepsForRound(roundIndex);
        return (recipe.Dish, recipe.Seq.Take(len).ToArray());
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1º",
        2 => "2º",
        3 => "3º",
        _ => $"{n}º"
    };
}