using Discord.Commands;

namespace GorillazDiscordBot.Commands;

public class FunModule : ModuleBase<SocketCommandContext>
{
    private static readonly Random _random = new();

    [Command("ping")]
    public async Task PingAsync() => await ReplyAsync("Pong!");

    [Command("mama")]
    public async Task MamaAsync()
    {
        for (int i = 0; i < 10; i++)
            await ReplyAsync("Glub!");
        await ReplyAsync("Engasguei!");
    }

    [Command("banana")]
    public async Task BananaAsync()
    {
        for (int i = 0; i < 3; i++)
            await ReplyAsync("Cadê?!");
        await ReplyAsync("Bananinha gostosa?!");
    }

    [Command("8ball")]
    public async Task EightBallAsync([Remainder] string pergunta)
    {
        string[] respostas =
        {
            "Com certeza!", "Não conte com isso.", "Talvez, quem sabe?",
            "Sem dúvida.", "Pergunte novamente mais tarde.",
            "Minha resposta é não.", "É provável.", "Não posso prever agora."
        };

        int idx = _random.Next(respostas.Length);
        await ReplyAsync($"🎱 Pergunta: {pergunta}\nResposta: **{respostas[idx]}**");
    }

    [Command("gorila")]
    public async Task GorilaAsync()
    {
        var curiosidades = new[]
        {
            "Gorilas podem rir, chorar e até usar ferramentas!",
            "Eles vivem em grupos liderados por um macho dominante chamado 'prata-costas'.",
            "Gorilas são 98% geneticamente parecidos com humanos.",
            "Eles são herbívoros e podem comer até 40 libras de comida por dia."
        };

        int idx = _random.Next(curiosidades.Length);
        await ReplyAsync($"🦍 Curiosidade: {curiosidades[idx]}");
    }

    [Command("dado")]
    public async Task DiceAsync()
    {
        int resultado = _random.Next(1, 7);
        await ReplyAsync($"🎲 Você rolou: **{resultado}**");
    }

    [Command("flip")]
    public async Task FlipAsync()
    {
        var resultado = _random.Next(0, 2) == 0 ? "Cara" : "Coroa";
        await ReplyAsync($"🪙 O resultado foi: **{resultado}**");
    }
}
