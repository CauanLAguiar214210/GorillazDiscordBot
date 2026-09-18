using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services.Interfaces;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Interaction;

[Group("interacao", "Respostas automáticas do servidor (triggers do chat com texto, GIF, áudio ou vídeo)")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class InteractionSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string CustomIdPrefix = "int";
    private const string PrevAction = "prev";
    private const string NextAction = "next";

    private const int PageSize = 10;
    private const int MaxResponseLength = 2000;
    private const int TruncateLength = 40;

    private static readonly Regex TriggerRegex = new(@"^[a-z0-9_-]{1,32}$", RegexOptions.Compiled);

    private readonly IGuildInteractionRepository _interactionRepository;
    private readonly IGifUrlService _gifUrlService;

    public InteractionSlashModule(
        IGuildInteractionRepository interactionRepository,
        IGifUrlService gifUrlService)
    {
        _interactionRepository = interactionRepository;
        _gifUrlService = gifUrlService;
    }

    [SlashCommand("criar", "Adiciona uma interação: o trigger responde com texto, GIF, áudio ou vídeo")]
    public async Task CriarAsync(
        [Summary("trigger", "Palavra que dispara a resposta (minúsculas, números, - ou _)")] string trigger,
        [Summary("resposta", "Texto ou URL da mídia (depende do tipo)")] string resposta,
        [Summary("tipo", "Tipo de resposta")] GuildInteractionType tipo = GuildInteractionType.Texto)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        trigger = trigger.ToLowerInvariant();
        if (!TriggerRegex.IsMatch(trigger))
        {
            await RespondAsync("❌ Trigger inválido. Use apenas letras minúsculas, números, `-` ou `_` (máx. 32 caracteres).");
            return;
        }

        var response = resposta.Trim();

        if (tipo == GuildInteractionType.Texto)
        {
            if (string.IsNullOrEmpty(response) || response.Length > MaxResponseLength)
            {
                await RespondAsync($"❌ A resposta deve ter entre 1 e {MaxResponseLength} caracteres.");
                return;
            }
        }
        else
        {
            try
            {
                response = await _gifUrlService.GetDirectMediaUrlAsync(response, tipo);
            }
            catch (Exception ex)
            {
                await RespondAsync($"❌ {ex.Message}");
                return;
            }
        }

        var interaction = new GuildInteraction
        {
            GuildId = Context.Guild.Id,
            Trigger = trigger,
            Tipo = tipo,
            Response = response,
            AddedBy = Context.User.Id,
            CreatedAt = DateTime.UtcNow
        };

        var added = await _interactionRepository.AddAsync(interaction);
        if (!added)
        {
            await RespondAsync($"❌ A interação `{trigger}` já existe neste servidor.");
            return;
        }

        var midiaDesc = tipo switch
        {
            GuildInteractionType.Gif => "um GIF",
            GuildInteractionType.Audio => "um áudio",
            GuildInteractionType.Video => "um vídeo",
            _ => "uma mensagem"
        };

        await RespondAsync($"✅ Interação `{trigger}` adicionada!\nAgora escrever `{trigger}` no chat responde com **{midiaDesc}**.");
    }

    [SlashCommand("remover", "Remove uma interação do servidor")]
    public async Task RemoverAsync(
        [Summary("trigger", "Trigger da interação a remover")] string trigger)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        trigger = trigger.ToLowerInvariant();
        var removed = await _interactionRepository.RemoveAsync(Context.Guild.Id, trigger);
        if (!removed)
        {
            await RespondAsync($"❌ A interação `{trigger}` não existe neste servidor.");
            return;
        }

        await RespondAsync($"✅ Interação `{trigger}` removida.");
    }

    [SlashCommand("listar", "Lista as interações configuradas no servidor")]
    public async Task ListarAsync()
    {
        var interactions = (await _interactionRepository.GetAllAsync(Context.Guild.Id))
            .OrderBy(i => i.Trigger)
            .ToList();

        if (interactions.Count == 0)
        {
            await RespondAsync("Este servidor ainda não tem interações. Use `/interacao criar` para adicionar.");
            return;
        }

        var (embed, components) = BuildPage(interactions, page: 1, Context.User.Id);
        await RespondAsync(embed: embed, components: components);
    }

    [ComponentInteraction(CustomIdPrefix + ":" + PrevAction + ":*:*", true)]
    public async Task PrevPageAsync(string page, string ownerId)
        => await ChangePageAsync(page, ownerId, -1);

    [ComponentInteraction(CustomIdPrefix + ":" + NextAction + ":*:*", true)]
    public async Task NextPageAsync(string page, string ownerId)
        => await ChangePageAsync(page, ownerId, +1);

    private async Task ChangePageAsync(string pageArg, string ownerIdArg, int direction)
    {
        await DeferAsync();

        if (!ulong.TryParse(ownerIdArg, out var owner) || owner != Context.User.Id)
        {
            await FollowupAsync("📖 Use `/interacao listar` para abrir sua própria listagem.", ephemeral: true);
            return;
        }

        var current = int.TryParse(pageArg, out var parsed) ? parsed : 1;

        var interactions = (await _interactionRepository.GetAllAsync(Context.Guild.Id))
            .OrderBy(i => i.Trigger)
            .ToList();

        if (interactions.Count == 0)
            return;

        var target = Math.Clamp(current + direction, 1, TotalPages(interactions.Count));
        var (embed, components) = BuildPage(interactions, target, Context.User.Id);

        var component = (SocketMessageComponent)Context.Interaction;
        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    private static (Embed Embed, MessageComponent Components) BuildPage(
        List<GuildInteraction> interactions, int page, ulong ownerId)
    {
        var totalPages = TotalPages(interactions.Count);
        page = Math.Clamp(page, 1, totalPages);

        var pageItems = interactions
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        var sb = new StringBuilder();
        foreach (var item in pageItems)
            sb.AppendLine($"{TypeIcon(item.Tipo)} `{item.Trigger}` → {TruncateResponse(item.Response)}");

        var embed = new EmbedBuilder()
            .WithTitle("💬 Interações do servidor")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter($"Página {page}/{totalPages} · Total: {interactions.Count} · Use /interacao criar")
            .Build();

        var builder = new ComponentBuilder();
        if (totalPages > 1)
        {
            builder
                .WithButton("⬅️ Anterior", $"{CustomIdPrefix}:{PrevAction}:{page}:{ownerId}", ButtonStyle.Secondary, disabled: page <= 1)
                .WithButton("Próxima ➡️", $"{CustomIdPrefix}:{NextAction}:{page}:{ownerId}", ButtonStyle.Secondary, disabled: page >= totalPages);
        }

        return (embed, builder.Build());
    }

    private static int TotalPages(int count)
        => Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));

    private static string TypeIcon(GuildInteractionType tipo)
        => tipo switch
        {
            GuildInteractionType.Gif => "🖼️",
            GuildInteractionType.Audio => "🎵",
            GuildInteractionType.Video => "🎬",
            _ => "💬"
        };

    private static string TruncateResponse(string response)
        => response.Length <= TruncateLength
            ? response
            : $"{response[..TruncateLength].TrimEnd()}…";
}