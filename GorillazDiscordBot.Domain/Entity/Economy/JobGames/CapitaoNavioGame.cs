namespace GorillazDiscordBot.Domain.Entity.Economy;

public sealed record CapitaoNavioManiobra(string Maneuver, string Traffic, bool IsSafe)
{
    public bool HasConflict => !IsSafe;
}

public static class CapitaoNavioGame
{
    public const ulong BasePerRound = 310;
    public const ulong ConflictBonus = 80;
    public const double ConflictChance = 0.5;

    private const int SafeIndex = 0;
    private const int ConflictIndex = 1;
    private static readonly string[] Decisions = { "\u2705 Aprovar", "\u26D4 Rejeitar" };

    private static readonly CapitaoNavioManiobra[] Cases =
    {
        new("Aproximação ao berço 12 a 8 nós", "Berço livre e mar calmo", true),
        new("Atravessar o canal estreito", "Cargueiro em sentido contrário a 0,5 milha", false),
        new("Fondear à direita do canal", "Fundo rochoso demarcado na carta", false),
        new("Navegação noturna com luzes acesas", "Tráfego leve e visibilidade boa", true),
        new("Manobra de atracação com reboque", "Rebocador autorizado em posição", true),
        new("Seguir rumo 040 no corredor de saída", "Navio de pesca fechando à proa", false),
        new("Descarte de lastro no fundeadouro", "Área demarcada de descarte de lastro", true),
        new("Reduzir a 6 nós no trecho com banhistas", "Balizamento costeiro visível", true),
        new("Ultrapassar por estibordo no alinhamento", "Embarcação à tona a 2 m do casco", false),
        new("Manter a velocidade ao passar pela doca", "Onda de proa atingiria embarcações ao largo", false)
    };

    private static readonly CapitaoNavioManiobra[] TrickyCases =
    {
        new("Manter rumo 270 contornando o boiar 4", "Folga de 6 m; o plano de navegação previa 15 m", false),
        new("Seguir o cargueiro à frente pelo canal", "Distância de seguimento dentro do limite regulamentar", true),
        new("Atracar no berço 7 com vento de través", "Proa estabilizada e teste de máquina concluído", true),
        new("Alterar para bombordo no último trecho", "Margem de segurança menor que a drafts da carta", false)
    };

    public static CapitaoNavioManiobra Build(int roundIndex, Random rng)
    {
        var pool = roundIndex <= 1 ? Cases : Cases.Concat(TrickyCases).ToArray();
        var safe = pool.Where(c => c.IsSafe).ToArray();
        var conflict = pool.Where(c => c.HasConflict).ToArray();

        return rng.NextDouble() < ConflictChance
            ? conflict[rng.Next(conflict.Length)]
            : safe[rng.Next(safe.Length)];
    }

    public static JobGameRound ToRound(CapitaoNavioManiobra maniobra)
    {
        var prompt =
            "\U0001F6A2 **Tráfego Marítimo** — Ordens do oficial\n\n" +
            $"Manobra: **{maniobra.Maneuver}**\n" +
            $"Tráfego: {maniobra.Traffic}\n\n" +
            "A manobra é segura?";

        return maniobra.HasConflict
            ? new JobGameRound(prompt, Decisions, ConflictIndex, "Conflito detectado", ConflictBonus)
            : new JobGameRound(prompt, Decisions, SafeIndex);
    }

    public static JobGameRound Step(int roundIndex, Random rng)
        => ToRound(Build(roundIndex, rng));
}