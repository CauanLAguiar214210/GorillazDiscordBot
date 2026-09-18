namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class ComandanteIateGame
{
    public const ulong BasePerRound = 230;
    public const int SequenceLength = 4;

    private static readonly string[][] Checklists =
    {
        new[]
        {
            "Conferir combustível e água doce",
            "Inspecionar coletes e extintores",
            "Testar motores e a reversa",
            "Soltar os cabos e zarpar"
        },
        new[]
        {
            "Verificar a previsão do tempo",
            "Planejar a rota na carta náutica",
            "Conferir rádio e GPS",
            "Delegar postos à tripulação"
        },
        new[]
        {
            "Conferir geladeira e provisões",
            "Checar as luzes de navegação",
            "Fechar portas e escotilhas",
            "Comunicar a saída ao porto"
        },
        new[]
        {
            "Inspecionar âncora e cabo",
            "Testar a bomba de porão",
            "Conferir extintores e flare",
            "Acertar o trim da embarcação"
        }
    };

    public static JobGameRound Step(int roundIndex, int stepIndex, Random rng)
    {
        var seeded = new Random(roundIndex * 1000 + stepIndex * 7 + 3);
        var steps = Checklists[roundIndex % Checklists.Length];

        var options = steps.OrderBy(_ => seeded.Next()).ToList();
        var required = steps[stepIndex];

        return new JobGameRound(
            "\U0001F6E5 **Checklist de Zarpe** — Execute o próximo passo na ordem:",
            options,
            options.IndexOf(required));
    }
}