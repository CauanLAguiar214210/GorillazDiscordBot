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
            await ReplyAsync("📖 Use: `macaco gif <nome>`, `macaco gif add <nome> <url>`, ou `macaco gif random`");
            return;
        }

        if (input.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            await SendRandomGifAsync();
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

        var gifs = await _gifRepository.GetAllAsync();
        if (gifs.Count == 0)
        {
            await ReplyAsync("📭 Nenhum GIF encontrado. Use `macaco gif add <nome> <url>` para adicionar.");
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
}
