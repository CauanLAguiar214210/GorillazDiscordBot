using Discord.Commands;
using System.Text;

namespace GorillazDiscordBot.Commands;

public class UtilityModule : ModuleBase<SocketCommandContext>
{
    private static readonly Random _random = new();

    [Command("ajuda")]
    public async Task AjudaAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🛠️ **Comandos disponíveis:**");
        sb.AppendLine();
        sb.AppendLine("**ping** — Responde com Pong!");
        sb.AppendLine("**ajuda** — Mostra esta mensagem de ajuda.");
        sb.AppendLine("**mama** — Responde com uma sequência de 'Glub!' e termina com 'Engasguei!'.");
        sb.AppendLine("**banana** — Responde com 'Cadê?!' repetido e finaliza com 'Bananinha gostosa?!'.");
        sb.AppendLine("**azul** — Mostra imagens aleatórias com frases divertidas.");
        sb.AppendLine("**joia** — Envia um GIF animado.");
        sb.AppendLine("**malicia** — Envia outro GIF animado engraçado.");
        sb.AppendLine("**galado** — Envia um GIF de raposa animada.");
        sb.AppendLine("**fazol** — Envia GIFs variados aleatórios.");
        sb.AppendLine("**senta** — Envia GIF e frase para 'sentar aqui'.");
        sb.AppendLine("**cotacao** — Mostra a cotação atual de várias moedas.");
        sb.AppendLine();
        sb.AppendLine("**dado** — Joga um dado de 6 lados e mostra o resultado.");
        sb.AppendLine("**flip** — Joga cara ou coroa.");
        sb.AppendLine("**8ball [pergunta]** — Responde sua pergunta com a Bola 8 mágica.");
        sb.AppendLine("**userinfo** — Exibe informações básicas sobre você.");
        sb.AppendLine("**tempo [cidade]** — Mostra a previsão do tempo para uma cidade (ainda não implementado).");
        sb.AppendLine("**random [min] [max]** — Gera um número aleatório entre dois valores.");
        sb.AppendLine("**gorila** — Envia uma curiosidade sobre gorilas.");
        sb.AppendLine("**timer [segundos]** — Define um temporizador em segundos e avisa quando terminar.");
        sb.AppendLine("**avatar [usuário]** — Mostra o avatar do usuário mencionado ou seu próprio avatar.");
        sb.AppendLine("**serverinfo** — Mostra informações básicas do servidor.");
        sb.AppendLine("**horario** — Mostra a hora atual do servidor (UTC).");
        sb.AppendLine("**contador [número]** — Conta de 1 até o número informado (máximo 20).");
        sb.AppendLine("**reversa [texto]** — Retorna a mensagem invertida.");

        await ReplyAsync(sb.ToString());
    }

    [Command("userinfo")]
    public async Task UserInfoAsync()
    {
        var user = Context.User;
        string info = $"👤 Usuário: {user.Username}#{user.Discriminator}\n" +
                      $"ID: {user.Id}\n" +
                      $"Criado em: {user.CreatedAt.DateTime:dd/MM/yyyy}\n";

        await ReplyAsync(info);
    }

    [Command("random")]
    public async Task RandomAsync(int min, int max)
    {
        if (min > max)
        {
            await ReplyAsync("O valor mínimo deve ser menor ou igual ao máximo.");
            return;
        }

        int numero = _random.Next(min, max + 1);
        await ReplyAsync($"🎲 Número aleatório entre {min} e {max}: **{numero}**");
    }

    [Command("timer")]
    public async Task TimerAsync(int segundos)
    {
        if (segundos <= 0)
        {
            await ReplyAsync("⏳ Por favor, informe um tempo válido em segundos.");
            return;
        }

        await ReplyAsync($"⏳ Temporizador iniciado: {segundos} segundos.");
        await Task.Delay(segundos * 1000);
        await ReplyAsync($"{Context.User.Mention} ⏰ Tempo esgotado!");
    }

    [Command("avatar")]
    public async Task AvatarAsync(Discord.IUser? user = null)
    {
        user ??= Context.User;
        var avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
        await ReplyAsync($"{user.Username}'s avatar: {avatarUrl}");
    }

    [Command("serverinfo")]
    public async Task ServerInfoAsync()
    {
        var guild = Context.Guild;
        if (guild == null)
        {
            await ReplyAsync("Este comando só pode ser usado dentro de um servidor.");
            return;
        }

        var info = new StringBuilder();
        info.AppendLine($"🏰 Nome do servidor: {guild.Name}");
        info.AppendLine($"👥 Membros: {guild.MemberCount}");
        info.AppendLine($"🌐 Região: {guild.VoiceRegionId}");
        info.AppendLine($"📅 Criado em: {guild.CreatedAt.DateTime:dd/MM/yyyy}");
        info.AppendLine($"🛡️ Dono: {guild.Owner.Username}#{guild.Owner.Discriminator}");

        await ReplyAsync(info.ToString());
    }

    [Command("horario")]
    public async Task HorarioAsync()
    {
        var horaAtual = DateTime.UtcNow;
        await ReplyAsync($"⏰ Hora atual (UTC): {horaAtual:HH:mm:ss}");
    }

    [Command("contador")]
    public async Task ContadorAsync(int max)
    {
        if (max < 1 || max > 20)
        {
            await ReplyAsync("Por favor, escolha um número entre 1 e 20.");
            return;
        }

        var sb = new StringBuilder();
        for (int i = 1; i <= max; i++)
            sb.Append(i).Append(' ');

        await ReplyAsync(sb.ToString());
    }

    [Command("reversa")]
    public async Task ReversaAsync([Remainder] string texto)
    {
        var arr = texto.ToCharArray();
        Array.Reverse(arr);
        await ReplyAsync(new string(arr));
    }
}
