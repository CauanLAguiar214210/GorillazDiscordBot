namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class FaxineiroGame
{
    public const ulong BasePerRound = 50;

    private static readonly string[] Tools =
    {
        "\U0001F9FD", "\U0001F9F9", "\U0001F9F4", "\U0001F9FB",
        "\U0001F6ED\uFE0F", "\U0001F4A6"
    };

    private static readonly (string Dirt, string Tool, string ToolName)[] Jobs =
    {
        ("🍝 Gordura", "\U0001F9FD", "Esponja"),
        ("🪨 Poeira", "\U0001F9F9", "Vassoura"),
        ("🦠 Germes", "\U0001F9F4", "Desinfetante"),
        ("💧 Mancha", "\U0001F9FB", "Pano"),
        ("🗑️ Lixeira cheia", "\U0001F6ED\uFE0F", "Saco de lixo"),
        ("🪟 Vidro sujo", "\U0001F4A6", "Pano úmido"),
    };

    public static JobGameRound Step(Random rng)
    {
        var j = Jobs[rng.Next(Jobs.Length)];
        var options = Tools
            .Where(t => t != j.Tool)
            .OrderBy(_ => rng.Next())
            .Take(3)
            .Append(j.Tool)
            .OrderBy(_ => rng.Next())
            .ToList();

        return new JobGameRound(
            $"🧹 **Limpeza** — Remova **{j.Dirt}** usando:",
            options,
            options.IndexOf(j.Tool));
    }
}