namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class CondutorLanchaGame
{
    public const ulong BasePerRound = 150;

    private static readonly (string Situation, string Correct, string[] Wrong)[] Scenarios =
    {
        ("\U0001F30A Ondas fortes se aproximam no mar aberto", "Retornar ao porto seguro",
            new[] { "Atravessar na máxima velocidade", "Ignorar e seguir o plano", "Ancorar em águas profundas" }),
        ("🟢 Boia verde visível a boreste", "Passar mantendo-a à estibordo",
            new[] { "Passar por bombordo", "Ancorar perto da boia", "Virar a proa para a boia" }),
        ("\U0001F32B Neblina reduzindo a visibilidade", "Reduzir a velocidade e soar a buzina",
            new[] { "Aumentar a velocidade", "Desligar as luzes de navegação", "Seguir em velocidade de cruzeiro" }),
        ("\U0001F6DF Nadadores a poucos metros da lancha", "Afastar-se e reduzir a velocidade",
            new[] { "Acelerar para sair rápido", "Buzinar repetidamente", "Aproximar ainda mais" }),
        ("\u26FD Combustível em menos da metade", "Planejar o retorno antes de seguir",
            new[] { "Ignorar e seguir em frente", "Acelerar ao máximo", "Desligar o rádio" }),
        ("\U0001F6A4 Outra embarcação em rumo de colisão", "Mudar o rumo e manter distância",
            new[] { "Manter o rumo atual", "Acelerar para cruzar à frente", "Apagar as luzes de navegação" }),
        ("\u267B Alaerta de manutenção no painel", "Conferir o item antes de zarpar",
            new[] { "Partir imediatamente", "Ignorar o alerta", "Desligar o painel" }),
        ("\U0001F3DD Recife raso sinalizado à proa", "Desviar por águas mais profundas",
            new[] { "Passar direto por cima", "Acelerar sobre o recife", "Ancorar sobre o sinal" }),
    };

    public static JobGameRound Step(Random rng)
    {
        var scenario = Scenarios[rng.Next(Scenarios.Length)];
        var options = scenario.Wrong
            .OrderBy(_ => rng.Next())
            .Append(scenario.Correct)
            .OrderBy(_ => rng.Next())
            .ToList();

        return new JobGameRound(
            $"\U0001F6A4 **Navegação Costeira** — {scenario.Situation}:",
            options,
            options.IndexOf(scenario.Correct));
    }
}