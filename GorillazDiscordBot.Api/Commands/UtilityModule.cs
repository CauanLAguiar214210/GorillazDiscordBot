using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Utils;
using System.Text;

namespace GorillazDiscordBot.Commands;

public class UtilityModule : ModuleBase<SocketCommandContext>
{
    private readonly CommandService _commandService;
    private readonly DiscordSocketClient _client;
    private readonly IGuildInteractionRepository _interactionRepository;

    private static readonly Dictionary<string, (string Name, string Emoji)> _moduleInfo = new()
    {
        ["FunModule"] = ("Diversão", "🎮"),
        ["UtilityModule"] = ("Utilidades", "🛠️"),
        ["EconomyModule"] = ("Economia", "💰"),
        ["GifManageModule"] = ("GIFs", "🖼️"),
        ["ApiModule"] = ("APIs Externas", "🌐"),
        ["GuildModule"] = ("Boas-vindas & Despedidas", "👋"),
        ["VoiceModule"] = ("Canais de Voz", "🔊"),
        ["InteractionModule"] = ("Interações", "💬"),
    };

    private static readonly Dictionary<string, string> _commandDescriptions = new()
    {
        ["ping"] = "Responde com Pong!",
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
        ["welcome"] = "Configura o canal de boas-vindas",
        ["goodbye"] = "Configura o canal de despedidas",
        ["welcomemsg"] = "Define a mensagem de boas-vindas",
        ["goodbyemsg"] = "Define a mensagem de despedida",
        ["welcome off"] = "Desativa as mensagens de boas-vindas",
        ["goodbye off"] = "Desativa as mensagens de despedida",
        ["welcome config"] = "Mostra a configuração de boas-vindas",
        ["voice setup"] = "Define o canal criador de voz",
        ["voice off"] = "Desativa a criação automática de canais de voz",
        ["voice config"] = "Mostra a configuração de canais de voz",
        ["interaction add"] = "Adiciona uma interação do servidor",
        ["interaction remove"] = "Remove uma interação do servidor",
        ["interaction list"] = "Lista as interações do servidor",
    };

    public UtilityModule(
        CommandService commandService,
        DiscordSocketClient client,
        IGuildInteractionRepository interactionRepository)
    {
        _commandService = commandService;
        _client = client;
        _interactionRepository = interactionRepository;
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
            .WithBlurpleTheme()
            .WithAuthor($"{_client.CurrentUser.Username} — Comandos", _client.CurrentUser.GetAvatarUrl())
            .WithStandardFooter($"Use {mention} <comando> para mais detalhes");

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

        if (Context.Guild != null)
        {
            var interactions = await _interactionRepository.GetAllAsync(Context.Guild.Id);
            if (interactions.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var interaction in interactions.OrderBy(i => i.Trigger))
                    sb.AppendLine($"`{interaction.Trigger}` → {interaction.Response}");

                embed.AddField("💬 Interações deste servidor", sb.ToString(), inline: false);
            }

            embed.WithFooter("Dica: use `macaco interaction add <trigger> <resposta>` para criar suas próprias interações!");
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

        int numero = Random.Shared.Next(min, max + 1);
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
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var guild = Context.Guild;

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
