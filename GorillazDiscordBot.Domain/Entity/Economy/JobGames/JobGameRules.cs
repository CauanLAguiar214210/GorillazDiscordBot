namespace GorillazDiscordBot.Domain.Entity.Economy;

public enum JobGameKind
{
    Cozinheiro,
    Porteiro,
    Entregador,
    Faxineiro,
    Professor,
    CondutorLancha,
    ComandanteIate,
    CapitaoNavio
}

public sealed record JobGameRound(
    string Prompt,
    IReadOnlyList<string> Options,
    int CorrectIndex,
    string? Event = null,
    ulong Bonus = 0)
{
    public string CorrectOption => Options[CorrectIndex];
    public bool IsValid => CorrectIndex >= 0 && CorrectIndex < Options.Count;
}

public static class JobGameRules
{
    public const int RoundsPerSession = 8;
    public const int MaxErrors = 3;
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(15);

    public const double ComboStep = 0.20;
    public const double ComboMaxBonus = 1.00;

    public const double GorjetaChance = 0.18;
    public const ulong GorjetaReward = 20;
    public const double VipChance = 0.03;
    public const double VipMultiplier = 3.0;

    public static double ComboMultiplier(int combo)
        => 1.0 + Math.Min(Math.Max(0, combo - 1) * ComboStep, ComboMaxBonus);
}

public static class JobGameCatalog
{
    public static JobGameKind? ForJobKey(string jobKey) => jobKey.ToLowerInvariant() switch
    {
        "cozinheiro" => JobGameKind.Cozinheiro,
        "porteiro" => JobGameKind.Porteiro,
        "entregador" => JobGameKind.Entregador,
        "faxineiro" => JobGameKind.Faxineiro,
        "professor" => JobGameKind.Professor,
        "condutor-lancha" => JobGameKind.CondutorLancha,
        "comandante-iate" => JobGameKind.ComandanteIate,
        "capitao-navio" => JobGameKind.CapitaoNavio,
        _ => null
    };

    public static (string Title, string Emoji) Meta(JobGameKind kind) => kind switch
    {
        JobGameKind.Cozinheiro => ("Receita na Memória", "\U0001F9D1\u200D\U0001F373"),
        JobGameKind.Porteiro => ("Portaria", "\U0001F6AA"),
        JobGameKind.Entregador => ("Etiqueta Certa", "\U0001F6B5"),
        JobGameKind.Faxineiro => ("Comando de Limpeza", "\U0001F9F9"),
        JobGameKind.Professor => ("Correção de Provas", "\U0001F4DD"),
        JobGameKind.CondutorLancha => ("Navegação Costeira", "\U0001F6A4"),
        JobGameKind.ComandanteIate => ("Checklist de Zarpe", "\U0001F6E5"),
        JobGameKind.CapitaoNavio => ("Tráfego Marítimo", "\U0001F6A2"),
        _ => ("Mini-jogo", "\U0001F3AE")
    };

    public static ulong BasePerRound(JobGameKind kind) => kind switch
    {
        JobGameKind.Cozinheiro => CozinheiroGame.BasePerRound,
        JobGameKind.Porteiro => PorteiroGame.BasePerRound,
        JobGameKind.Entregador => EntregadorGame.BasePerRound,
        JobGameKind.Faxineiro => FaxineiroGame.BasePerRound,
        JobGameKind.Professor => ProfessorGame.BasePerRound,
        JobGameKind.CondutorLancha => CondutorLanchaGame.BasePerRound,
        JobGameKind.ComandanteIate => ComandanteIateGame.BasePerRound,
        JobGameKind.CapitaoNavio => CapitaoNavioGame.BasePerRound,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool IsMemory(JobGameKind kind) => kind == JobGameKind.Cozinheiro;

    public static int RoundsFor(JobGameKind kind)
        => kind == JobGameKind.Professor ? ProfessorGame.MaxQuestions : JobGameRules.RoundsPerSession;

    public static int ClampRounds(JobGameKind kind, int count)
        => kind == JobGameKind.Professor ? ProfessorGame.ClampCount(count) : count;

    public static IReadOnlyList<JobGameRound>? PrepareRounds(JobGameKind kind, int count, Random rng)
        => kind == JobGameKind.Professor ? ProfessorGame.BuildExam(count, rng) : null;

    public static int StepsForRound(JobGameKind kind, int roundIndex)
        => IsMemory(kind)
            ? CozinheiroGame.StepsForRound(roundIndex)
            : kind == JobGameKind.ComandanteIate ? ComandanteIateGame.SequenceLength : 1;

    public static string? Reveal(JobGameKind kind, int roundIndex)
        => IsMemory(kind) ? CozinheiroGame.Reveal(roundIndex) : null;

    public static JobGameRound Round(JobGameKind kind, int roundIndex, int stepIndex, Random rng) => kind switch
    {
        JobGameKind.Cozinheiro => CozinheiroGame.Step(roundIndex, stepIndex, rng),
        JobGameKind.Porteiro => PorteiroGame.Step(roundIndex, rng),
        JobGameKind.Entregador => EntregadorGame.Step(rng),
        JobGameKind.Faxineiro => FaxineiroGame.Step(rng),
        JobGameKind.Professor => ProfessorGame.Step(rng),
        JobGameKind.CondutorLancha => CondutorLanchaGame.Step(rng),
        JobGameKind.ComandanteIate => ComandanteIateGame.Step(roundIndex, stepIndex, rng),
        JobGameKind.CapitaoNavio => CapitaoNavioGame.Step(roundIndex, rng),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}