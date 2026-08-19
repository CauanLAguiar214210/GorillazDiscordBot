using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class InteractionModule : ModuleBase<SocketCommandContext>
{
    private static readonly Regex TriggerRegex = new(@"^[a-z0-9_-]{1,32}$", RegexOptions.Compiled);
    private const int MaxResponseLength = 2000;

    private readonly IGuildInteractionRepository _interactionRepository;

    public InteractionModule(IGuildInteractionRepository interactionRepository)
    {
        _interactionRepository = interactionRepository;
    }

    [Command("interaction add")]
    [Summary("Adiciona uma interação do servidor. Uso: macaco interaction add <trigger> <resposta>")]
    public async Task InteractionAddAsync(string trigger, [Remainder] string response)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        trigger = trigger.ToLowerInvariant();
        if (!TriggerRegex.IsMatch(trigger))
        {
            await ReplyAsync("❌ Trigger inválido. Use apenas letras minúsculas, números, `-` ou `_` (máx. 32 caracteres).");
            return;
        }

        response = response.Trim();
        if (string.IsNullOrEmpty(response) || response.Length > MaxResponseLength)
        {
            await ReplyAsync($"❌ A resposta deve ter entre 1 e {MaxResponseLength} caracteres.");
            return;
        }

        var interaction = new GuildInteraction
        {
            GuildId = Context.Guild.Id,
            Trigger = trigger,
            Response = response,
            AddedBy = Context.User.Id,
            CreatedAt = DateTime.UtcNow
        };

        var added = await _interactionRepository.AddAsync(interaction);
        if (!added)
        {
            await ReplyAsync($"❌ A interação `{trigger}` já existe neste servidor.");
            return;
        }

        await ReplyAsync($"✅ Interação `{trigger}` adicionada!\nAgora `macaco {trigger}` responde com a configuração definida.");
    }

    [Command("interaction remove")]
    [Summary("Remove uma interação do servidor. Uso: macaco interaction remove <trigger>")]
    public async Task InteractionRemoveAsync(string trigger)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        trigger = trigger.ToLowerInvariant();
        var removed = await _interactionRepository.RemoveAsync(Context.Guild.Id, trigger);
        if (!removed)
        {
            await ReplyAsync($"❌ A interação `{trigger}` não existe neste servidor.");
            return;
        }

        await ReplyAsync($"✅ Interação `{trigger}` removida.");
    }

    [Command("interaction list")]
    [Summary("Lista as interações configuradas no servidor")]
    public async Task InteractionListAsync()
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var interactions = await _interactionRepository.GetAllAsync(Context.Guild.Id);

        if (interactions.Count == 0)
        {
            await ReplyAsync("Este servidor não tem interações configuradas.");
            return;
        }

        var sb = new StringBuilder();
        foreach (var interaction in interactions.OrderBy(i => i.Trigger))
            sb.AppendLine($"`{interaction.Trigger}` → {interaction.Response}");

        var embed = new EmbedBuilder()
            .WithTitle("💬 Interações do servidor")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter($"Total: {interactions.Count} · Use macaco interaction add <trigger> <resposta>")
            .Build();

        await ReplyAsync(embed: embed);
    }
}
