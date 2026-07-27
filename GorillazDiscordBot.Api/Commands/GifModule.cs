using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Commands;

[Group("gif")]
public class GifManageModule : ModuleBase<SocketCommandContext>
{
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
        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyAsync("📖 Use: `macaco gif <nome>`, `macaco gif add <nome> <url>`, `macaco gif list`, `macaco gif random`, `macaco gif categories`, ou `macaco gif remove <nome>`");
            return;
        }

        if (input.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            await SendRandomGifAsync();
            return;
        }

        if (input.Equals("categories", StringComparison.OrdinalIgnoreCase))
        {
            await ListCategoriesAsync();
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
                await ReplyAsync("⚠️ Use: `macaco gif add <nome> <url>`");
            }
            return;
        }

        if (input.StartsWith("remove ", StringComparison.OrdinalIgnoreCase))
        {
            var nome = input[7..].Trim();
            await RemoveGifAsync(nome);
            return;
        }

        if (input.StartsWith("list", StringComparison.OrdinalIgnoreCase))
        {
            var args = input[4..].Trim();
            await ListGifsAsync(args);
            return;
        }

        await SearchGifAsync(input.Trim());
    }

    private async Task AddGifAsync(string nome, string url)
    {
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(url))
        {
            await ReplyAsync("⚠️ Use: `macaco gif add <nome> <url>`");
            return;
        }

        var existente = await _gifRepository.GetByNomeAsync(nome);
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
            Nome = nome,
            Url = resolvedUrl,
            AddedBy = Context.User.Id,
            AddedAt = DateTime.UtcNow
        };

        await _gifRepository.CreateAsync(gif);
        await ReplyAsync($"✅ GIF `{nome}` adicionado com sucesso!");
    }

    private async Task SendRandomGifAsync()
    {
        var gif = await _gifRepository.GetRandomAsync();

        if (gif != null)
            await ReplyAsync(gif.Url);
        else
            await ReplyAsync("📭 Nenhum GIF cadastrado ainda. Use `macaco gif add <nome> <url>` para adicionar.");
    }

    private async Task SearchGifAsync(string nome)
    {
        var gif = await _gifRepository.GetByNomeAsync(nome);

        if (gif != null)
        {
            await ReplyAsync(gif.Url);
            return;
        }

        var total = await _gifRepository.GetCountAsync();
        if (total == 0)
        {
            await ReplyAsync("📭 Nenhum GIF encontrado. Use `macaco gif add <nome> <url>` para adicionar.");
            return;
        }

        var todosGifs = await _gifRepository.GetAllAsync();
        var similares = todosGifs
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

    private async Task ListGifsAsync(string args)
    {
        const int pageSize = 15;
        var page = 1;
        string? categoriaFiltro = null;

        ParseListArgs(args, ref page, ref categoriaFiltro);

        var total = await _gifRepository.GetCountAsync(categoriaFiltro);

        if (total == 0)
        {
            var msg = string.IsNullOrEmpty(categoriaFiltro)
                ? "📭 Nenhum GIF cadastrado ainda. Use `macaco gif add <nome> <url>` para adicionar."
                : $"📭 Nenhum GIF encontrado na categoria `{categoriaFiltro}`.";
            await ReplyAsync(msg);
            return;
        }

        var totalPaginas = (int)Math.Ceiling((double)total / pageSize);

        if (page < 1) page = 1;
        if (page > totalPaginas) page = totalPaginas;

        var gifsPagina = await _gifRepository.GetPaginatedAsync(page, pageSize, categoriaFiltro);

        var embed = new EmbedBuilder()
            .WithColor(Color.Gold)
            .WithTitle("📚 GIFs Cadastrados");

        if (!string.IsNullOrEmpty(categoriaFiltro))
            embed.WithDescription($"Categoria: **{categoriaFiltro}** · {total} total");
        else
            embed.WithDescription($"{total} GIFs cadastrados");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        for (int i = 0; i < gifsPagina.Count; i++)
        {
            var gif = gifsPagina[i];
            var numero = (page - 1) * pageSize + i + 1;
            var usuario = Context.Client.GetUser(gif.AddedBy);
            var nomeUsuario = usuario?.Username ?? "Desconhecido";
            var data = gif.AddedAt.ToString("dd/MM/yyyy");
            sb.AppendLine($"**{numero}.** `{gif.Nome}` · @{nomeUsuario} · {data}");
        }

        var listaStr = sb.ToString();
        var currentDesc = embed.Description ?? "";

        if (currentDesc.Length + listaStr.Length > 4000)
        {
            listaStr = listaStr[..(4000 - currentDesc.Length)] + "\n... (lista truncada)";
        }

        embed.WithDescription(currentDesc + listaStr);

        if (totalPaginas > 1)
        {
            var proximaPagina = page < totalPaginas ? page + 1 : 1;
            embed.WithFooter($"Página {page}/{totalPaginas} · Próxima: macaco gif list --page {proximaPagina}");
        }
        else
        {
            embed.WithFooter($"Total: {total} GIFs");
        }

        embed.WithTimestamp(DateTimeOffset.UtcNow);

        await ReplyAsync(embed: embed.Build());
    }

    private async Task ListCategoriesAsync()
    {
        var categorias = await _gifRepository.GetCategoriasAsync();

        if (categorias.Count == 0)
        {
            await ReplyAsync("📭 Nenhuma categoria cadastrada. Adicione um gif com `macaco gif add <nome> <url>`");
            return;
        }

        var embed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithTitle("📂 Categorias Disponíveis")
            .WithDescription(string.Join("\n", categorias.Select(c => $"• `{c}`")))
            .WithFooter("Use 'macaco gif list --categoria <nome>' para filtrar")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await ReplyAsync(embed: embed);
    }

    private async Task RemoveGifAsync(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            await ReplyAsync("⚠️ Use: `macaco gif remove <nome>`");
            return;
        }

        var gif = await _gifRepository.GetByNomeAsync(nome);
        if (gif == null)
        {
            await ReplyAsync($"❌ GIF `{nome}` não encontrado.");
            return;
        }

        var deletado = await _gifRepository.DeleteByNomeAsync(nome);
        if (deletado)
        {
            await ReplyAsync($"✅ GIF `{nome}` removido com sucesso!");
        }
        else
        {
            await ReplyAsync($"❌ Erro ao remover o GIF `{nome}`.");
        }
    }

    private void ParseListArgs(string args, ref int page, ref string? categoriaFiltro)
    {
        if (string.IsNullOrWhiteSpace(args)) return;

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Equals("--page", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
            {
                if (int.TryParse(parts[i + 1], out var parsedPage))
                    page = parsedPage;
                i++;
            }
            else if (parts[i].Equals("--categoria", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
            {
                categoriaFiltro = parts[i + 1];
                i++;
            }
        }
    }
}
