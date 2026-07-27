using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Text;

namespace GorillazDiscordBot.Commands;

public class UtilityModule : ModuleBase<SocketCommandContext>
{
    private readonly CommandService _commandService;
    private readonly DiscordSocketClient _client;
    private static readonly Random _random = new();

    private static readonly Dictionary<string, (string Name, string Emoji)> _moduleInfo = new()
    {
        ["FunModule"] = ("Diversão", "🎮"),
        ["UtilityModule"] = ("Utilidades", "🛠️"),
        ["EconomyModule"] = ("Economia", "💰"),
        ["GifManageModule"] = ("GIFs", "🖼️"),
        ["ApiModule"] = ("APIs Externas", "🌐"),
    };

    private static readonly Dictionary<string, string> _commandDescriptions = new()
    {
        ["ping"] = "Responde com Pong!",
        ["mama"] = "Sequência de 'Glub!' até engasgar",
        ["banana"] = "'Cadê?!' repetido e finalização",
        ["8ball"] = "Bola 8 mágica responde sua pergunta",
        ["gorila"] = "Curiosidade sobre gorilas",
        ["dado"] = "Joga um dado de 6 lados",
        ["flip"] = "Cara ou coroa",
        ["daily"] = "Reivindica moedas diárias",
        ["saldo"] = "Ver seu saldo (alias: coins)",
        ["bet"] = "Aposta 50/50",
        ["pagar"] = "Transferir moedas para outro usuário",
        ["ranking"] = "Ranking de riqueza do servidor",
        ["gif"] = "Buscar, adicionar ou sortear GIFs",
        ["f1"] = "Classificação de pilotos de F1",
        ["cotacao"] = "Cotação de moedas",
        ["tempo"] = "Previsão do tempo por cidade",
        ["userinfo"] = "Suas informações de usuário",
        ["random"] = "Número aleatório entre min e max",
        ["timer"] = "Temporizador com aviso",
        ["avatar"] = "Avatar de um usuário",
        ["serverinfo"] = "Informações do servidor",
        ["horario"] = "Hora atual (UTC)",
        ["contador"] = "Conta de 1 até N",
        ["reversa"] = "Inverte o texto informado",
    };

    public UtilityModule(CommandService commandService, DiscordSocketClient client)
    {
        _commandService = commandService;
        _client = client;
    }

    [Command("ajuda")]
    [Alias("help")]
    public async Task AjudaAsync()
    {
        var mention = _client.CurrentUser.Mention;

        var modules = _commandService.Modules
            .Where(m => m.Commands.Count > 0)
            .OrderBy(m => m.Name)
            .ToList();

        var embed = new EmbedBuilder()
            .WithColor(new Color(0x5865F2))
            .WithAuthor($"{_client.CurrentUser.Username} — Comandos", _client.CurrentUser.GetAvatarUrl())
            .WithFooter($"Use {mention} <comando> para mais detalhes")
            .WithTimestamp(DateTimeOffset.UtcNow);

        foreach (var module in modules)
        {
            var (displayName, emoji) = _moduleInfo.GetValueOrDefault(
                module.Name, (module.Name, "📋"));

            var sb = new StringBuilder();
            foreach (var cmd in module.Commands)
            {
                var name = cmd.Aliases.FirstOrDefault() ?? cmd.Name;
                var desc = _commandDescriptions.GetValueOrDefault(name, "Sem descrição");
                sb.AppendLine($"`{name}` — {desc}");
            }

            embed.AddField($"{emoji} {displayName}", sb.ToString(), inline: false);
        }

        await ReplyAsync(embed: embed.Build());
    }

    [Command("ajuda")]
    public async Task AjudaAsync(string comando)
    {
        var cmd = _commandService.Commands
            .FirstOrDefault(c => c.Aliases.Any(a =>
                a.Equals(comando, StringComparison.OrdinalIgnoreCase)));

        if (cmd == null)
        {
            await ReplyAsync($"❌ Comando `{comando}` não encontrado.");
            return;
        }

        var name = cmd.Aliases.FirstOrDefault() ?? cmd.Name;
        var desc = _commandDescriptions.GetValueOrDefault(name, "Sem descrição");
        var aliases = cmd.Aliases.Where(a => a != name).ToList();
        var usage = cmd.Parameters.Count > 0
            ? $"`macaco {name} {string.Join(" ", cmd.Parameters.Select(p => $"<{p.Name}>"))}`"
            : $"`macaco {name}`";

        var embed = new EmbedBuilder()
            .WithColor(new Color(0x5865F2))
            .WithAuthor($"Comando: {name}", _client.CurrentUser.GetAvatarUrl())
            .WithDescription(desc)
            .AddField("Uso", usage, inline: false);

        if (aliases.Count > 0)
            embed.AddField("Aliases", string.Join(", ", aliases.Select(a => $"`{a}`")), inline: true);

        if (cmd.Parameters.Count > 0)
        {
            var paramList = string.Join("\n", cmd.Parameters.Select(p =>
                $"`{p.Name}` — {(string.IsNullOrEmpty(p.Summary) ? "Sem descrição" : p.Summary)}"));
            embed.AddField("Parâmetros", paramList, inline: false);
        }

        await ReplyAsync(embed: embed.Build());
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
