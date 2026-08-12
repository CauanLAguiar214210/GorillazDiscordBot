using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Commands;

[Group("gif")]
public class GifManageModule : ModuleBase<SocketCommandContext>
{
    private const int MaxListItems = 15;

    private readonly IGifRepository _gifRepository;
    private readonly IGifUrlService _gifUrlService;

    public GifManageModule(IGifRepository gifRepository, IGifUrlService gifUrlService)
    {
        _gifRepository = gifRepository;
        _gifUrlService = gifUrlService;
    }

    [Command]
    public async Task GifRouterAsync([Remainder] string input)
    {
        if (Context.Guild == null)
        {
            await ReplyAsync("⚠️ Os comandos de GIF devem ser usados dentro de um servidor.");
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyAsync("📖 Use: `gif <nome>`, `gif add <nome> <url>`, `gif random`, `gif list` ou `gif remove <nome>`");
            return;
        }

        if (input.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            await SendRandomGifAsync();
            return;
        }

        if (input.Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            await ListGifsAsync();
            return;
        }

        if (input.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var nome = input[7..].Trim();
            if (nome.Length == 0)
            {
                await ReplyAsync("⚠️ Use: `gif remove <nome>`");
                return;
            }

            await RemoveGifAsync(nome);
            return;
        }

        if (input.StartsWith("add ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = input[4..].Trim();
            var spaceIdx = rest.IndexOf(' ');
            if (spaceIdx > 0)
            {
                var nome = rest[..spaceIdx].Trim();
                var url = rest[(spaceIdx + 1)..].Trim();
                await AddGifAsync(nome, url);
            }
            else
            {
                await ReplyAsync("⚠️ Use: `gif add <nome> <url>`");
            }
            return;
        }

        await SearchGifAsync(input.Trim());
    }

    private async Task AddGifAsync(string nome, string url)
    {
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(url))
        {
            await ReplyAsync("⚠️ Use: `gif add <nome> <url>`");
            return;
        }

        var guildId = Context.Guild.Id;
        var existente = await _gifRepository.GetByNomeAsync(guildId, nome);
        if (existente != null)
        {
            await ReplyAsync($"⚠️ Já existe um GIF com o nome `{nome}`.");
            return;
        }

        string resolvedUrl;
        try
        {
            resolvedUrl = await _gifUrlService.GetDirectUrlAsync(url);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"⚠️ {ex.Message}");
            return;
        }

        var gif = new Entity.Gif
        {
            GuildId = guildId,
            Nome = nome,
            Url = resolvedUrl,
            AddedBy = Context.User.Id,
            AddedAt = DateTime.UtcNow
        };

        await _gifRepository.CreateAsync(gif);
        await ReplyAsync($"✅ GIF `{nome}` adicionado com sucesso!");
    }

    private async Task RemoveGifAsync(string nome)
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para remover GIFs.");
            return;
        }

        var removed = await _gifRepository.RemoveAsync(Context.Guild.Id, nome);

        if (removed)
            await ReplyAsync($"✅ GIF `{nome}` removido do servidor!");
        else
            await ReplyAsync($"❓ GIF `{nome}` não encontrado neste servidor.");
    }

    private async Task ListGifsAsync()
    {
        var gifs = await _gifRepository.GetAllAsync(Context.Guild.Id);

        if (gifs.Count == 0)
        {
            await ReplyAsync("📭 Nenhum GIF encontrado neste servidor. Use `gif add <nome> <url>` para adicionar.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🖼️ GIFs deste servidor")
            .WithColor(Color.Blue);

        foreach (var gif in gifs.Take(MaxListItems))
            embed.AddField(gif.Nome, gif.Url, false);

        if (gifs.Count > MaxListItems)
            embed.WithFooter($"Mostrando os primeiros {MaxListItems} de {gifs.Count} GIFs · {Context.Guild.Name}");
        else
            embed.WithFooter($"Total: {gifs.Count} GIFs · {Context.Guild.Name}");

        await ReplyAsync(embed: embed.Build());
    }

    private async Task SendRandomGifAsync()
    {
        var gif = await _gifRepository.GetRandomAsync(Context.Guild.Id);

        if (gif != null)
            await ReplyAsync(gif.Url);
        else
            await ReplyAsync("📭 Nenhum GIF cadastrado ainda. Use `gif add <nome> <url>` para adicionar.");
    }

    private async Task SearchGifAsync(string nome)
    {
        var gif = await _gifRepository.GetByNomeAsync(Context.Guild.Id, nome);

        if (gif != null)
        {
            await ReplyAsync(gif.Url);
            return;
        }

        var gifs = await _gifRepository.GetAllAsync(Context.Guild.Id);
        if (gifs.Count == 0)
        {
            await ReplyAsync("📭 Nenhum GIF encontrado. Use `gif add <nome> <url>` para adicionar.");
            return;
        }

        var similares = gifs
            .Where(g => g.Nome.Contains(nome, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (similares.Count > 0)
        {
            var sugestoes = string.Join(", ", similares.Select(g => $"`{g.Nome}`"));
            await ReplyAsync($"❓ GIF `{nome}` não encontrado. Você quis dizer: {sugestoes}?");
        }
        else
        {
            await ReplyAsync($"❓ GIF `{nome}` não encontrado.");
        }
    }

    private bool HasPermission()
        => Context.Guild != null &&
           Context.User is IGuildUser guildUser &&
           (guildUser.GuildPermissions.ManageGuild || guildUser.GuildPermissions.Administrator);
}
