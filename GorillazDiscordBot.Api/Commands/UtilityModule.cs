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
            var (displayName, emoji) = CommandCatalog.ModuleDisplay.GetValueOrDefault(
                module.Name, (module.Name, "📋"));

            var sb = new StringBuilder();
            foreach (var cmd in module.Commands)
            {
                var name = cmd.Aliases.FirstOrDefault() ?? cmd.Name;
                var desc = CommandCatalog.Descriptions.GetValueOrDefault(name, "Sem descrição");
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
        await ReplyAsync($"{user.GetDisplayName()}'s avatar: {avatarUrl}");
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
