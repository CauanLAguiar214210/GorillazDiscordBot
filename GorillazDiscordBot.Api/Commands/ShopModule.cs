using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class ShopModule : ModuleBase<SocketCommandContext>
{
    private readonly ShopService _shop;

    public ShopModule(ShopService shop)
    {
        _shop = shop;
    }

    [Command("loja")]
    [Alias("shop", "store")]
    [Summary("Mostra os itens disponíveis na loja")]
    public async Task LojaAsync()
    {
        var items = await _shop.GetCatalogAsync();
        var active = items.Where(i => i.IsActive).ToList();
        var real = active.Where(i => !i.IsPlaceholder).ToList();
        var placeholders = active.Where(i => i.IsPlaceholder).ToList();

        if (real.Count == 0 && placeholders.Count == 0)
        {
            await ReplyAsync("🛒 A loja está vazia no momento.");
            return;
        }

        var sb = new StringBuilder("🛒 **Loja do Gorillaz**\n\n");

        var groups = new[]
        {
            ("🎁", "Colecionáveis", ItemCategory.Cosmetic),
            ("⚡", "Boosts", ItemCategory.Boost),
            ("📈", "Ativos de Renda", ItemCategory.Asset),
        };

        foreach (var (icon, title, category) in groups)
        {
            var section = real.Where(i => i.Category == category).OrderByDescending(i => i.Price).ToList();
            if (section.Count == 0) continue;

            sb.AppendLine($"{icon} **{title}**");
            foreach (var item in section)
            {
                var extra = item.DurationHours > 0 ? $" *(dur. {item.DurationHours}h)*" : "";
                sb.AppendLine($"{item.Emoji} **{item.Name}** — **{EconomyFormat.Full(item.Price)}** moedas");
                sb.AppendLine($"   └ {item.Description}{extra}");
                if (item.Category == ItemCategory.Asset && item.DailyIncome > 0)
                    sb.AppendLine($"   └ Renda: **+{EconomyFormat.Full(item.DailyIncome)}/dia** no daily · limite: 1 por usuário");
                sb.AppendLine($"   └ id: `{item.Key}`");
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine();
        }

        var relics = real.Where(i => i.Category == ItemCategory.Relic).OrderByDescending(i => i.Price).ToList();
        if (relics.Count > 0)
        {
            sb.AppendLine("⌚ **Relógios Equipáveis** *(os mais caros da loja)*");
            foreach (var item in relics)
            {
                sb.AppendLine($"{item.Emoji} **{item.Name}** — **{EconomyFormat.Full(item.Price)}** moedas");
                sb.AppendLine($"   └ {item.Description}");
                sb.AppendLine($"   └ Efeito: {DescribeRelic(item)}");
                sb.AppendLine($"   └ id: `{item.Key}`");
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine();
        }

        if (placeholders.Count > 0)
        {
            sb.AppendLine("🔒 **Em breve**");
            foreach (var item in placeholders)
                sb.AppendLine($"{item.Emoji} **{item.Name}** — 🔒 Em breve");
            sb.AppendLine();
        }

        sb.AppendLine("Compre com `comprar <id>`. Equipe relógios com `equipar <id>`. Veja sua coleção com `inventario`.");

        foreach (var chunk in SplitMessage(sb.ToString(), 2000))
            await ReplyAsync(chunk);
    }

    private static string DescribeRelic(ShopItem item)
    {
        var target = item.RelicGame switch
        {
            RelicGameType.Roulette => "roleta",
            RelicGameType.Slots => "caça-níquel",
            RelicGameType.Blackjack => "blackjack",
            _ => "todos os jogos de cassino"
        };
        if (item.RelicEffect == RelicEffect.Cashback)
            return $"💸 Devolve **{item.RelicValue}%** da aposta na derrota ({target})";
        return $"📈 **+{item.RelicValue}%** nos ganhos ({target})";
    }

    private static IEnumerable<string> SplitMessage(string text, int maxLen)
    {
        while (text.Length > maxLen)
        {
            var split = text.LastIndexOf('\n', maxLen - 1);
            if (split <= 0) split = maxLen;
            yield return text[..split];
            text = text[split..].TrimStart('\n');
        }
        if (text.Length > 0) yield return text;
    }

    [Command("comprar")]
    [Alias("buy")]
    [Summary("Compra um item da loja. Uso: comprar <id>")]
    public async Task ComprarAsync([Remainder] string itemInput)
    {
        if (string.IsNullOrWhiteSpace(itemInput))
        {
            await ReplyAsync("❌ Uso: `comprar <id>`. Veja a loja com `loja`.");
            return;
        }

        var item = await _shop.FindItemAsync(itemInput);
        if (item == null || !item.IsActive)
        {
            await ReplyAsync("❌ Item não encontrado. Use `loja` para ver os ids disponíveis.");
            return;
        }

        var (success, message, _) = await _shop.BuyAsync(
            Context.User.Id, Context.User.Username, item);

        if (!success)
        {
            await ReplyAsync(message!);
            return;
        }

        await ReplyAsync($"✅ **{Context.User.GetDisplayName()}** comprou **{item.Emoji} {item.Name}** por **{EconomyFormat.Full(item.Price)}** moedas!");
    }

    [Command("vender")]
    [Alias("sell")]
    [Summary("Revende um item por reembolso parcial. Uso: vender <id>")]
    public async Task VenderAsync([Remainder] string itemInput)
    {
        if (string.IsNullOrWhiteSpace(itemInput))
        {
            await ReplyAsync("❌ Uso: `vender <id>`.");
            return;
        }

        var item = await _shop.FindItemAsync(itemInput);
        if (item == null)
        {
            await ReplyAsync("❌ Item não encontrado. Use `loja` para ver os ids.");
            return;
        }

        var (success, message, _) = await _shop.SellAsync(
            Context.User.Id, Context.User.Username, item);

        if (!success)
        {
            await ReplyAsync(message!);
            return;
        }

        var refund = (ulong)Math.Floor(item.Price * ShopService.SellRefundRate);
        await ReplyAsync($"💸 **{Context.User.GetDisplayName()}** vendeu **{item.Emoji} {item.Name}** e recebeu **{EconomyFormat.Full(refund)}** moedas de volta!");
    }

    [Command("usar")]
    [Alias("use")]
    [Summary("Ativa um boost. Uso: usar <id>")]
    public async Task UsarAsync([Remainder] string itemInput)
    {
        if (string.IsNullOrWhiteSpace(itemInput))
        {
            await ReplyAsync("❌ Uso: `usar <id>`.");
            return;
        }

        var item = await _shop.FindItemAsync(itemInput);
        if (item == null)
        {
            await ReplyAsync("❌ Item não encontrado. Use `loja` para ver os ids.");
            return;
        }

        var (success, message, _) = await _shop.UseAsync(
            Context.User.Id, Context.User.Username, item);

        if (!success)
        {
            await ReplyAsync(message!);
            return;
        }

        await ReplyAsync(item.Effect switch
        {
            BoostEffect.DailyX2 => $"⚡ **{Context.User.GetDisplayName()}** ativou **{item.Name}**! Seu **próximo daily** renderá o DOBRO!",
            BoostEffect.WorkX2 => $"💼 **{Context.User.GetDisplayName()}** ativou **{item.Name}**! Seu **próximo trabalho** renderá o DOBRO!",
            BoostEffect.RobShield => $"🛡️ **{Context.User.GetDisplayName()}** ativou o **{item.Name}** por **{item.DurationHours}h**!",
            _ => $"✅ Item usado!"
        });
    }

    [Command("inventario")]
    [Alias("inventory", "mochila", "bag")]
    [Summary("Mostra os itens que você possui")]
    public async Task InventarioAsync()
    {
        var ownership = await _shop.GetInventoryAsync(Context.User.Id);
        var catalog = await _shop.GetCatalogAsync();

        if (ownership.Count == 0)
        {
            await ReplyAsync($"🎒 **{Context.User.GetDisplayName()}**, seu inventário está vazio. Compre algo com `loja`!");
            return;
        }

        var sb = new StringBuilder($"🎒 **Inventário de {Context.User.GetDisplayName()}**\n\n");
        var byKey = ownership.ToDictionary(i => i.ItemKey, i => i);

        foreach (var pair in byKey)
        {
            var item = catalog.FirstOrDefault(c => c.Key == pair.Key);
            var name = item?.Name ?? pair.Key;
            var emoji = item?.Emoji ?? "❔";
            var qty = pair.Value.Quantity;
            var validade = pair.Value.ExpiresAt is { } exp && exp > DateTime.UtcNow
                ? $" *(expira {exp:dd/MM HH:mm} UTC)*"
                : qty > 1 ? "" : "";

            var linha = $"{emoji} **{name}** × **{qty}**{validade}";

            if (pair.Value.IsEquipped)
                linha += " · ⭐ **equipado**";

            if (item is { Category: ItemCategory.Asset, DailyIncome: > 0 })
            {
                var baseline = pair.Value.LastCollectedAt ?? pair.Value.AcquiredAt ?? DateTime.UtcNow;
                var dias = Math.Clamp((int)Math.Floor((DateTime.UtcNow - baseline).TotalDays), 0, ShopService.MaxIncomeBacklogDays);
                linha += $" · **+{EconomyFormat.Full(item.DailyIncome)}/dia** · {dias} dia(s) acumulados";
            }

            sb.AppendLine(linha);
        }

        await ReplyAsync(sb.ToString());
    }

    [Command("equipar")]
    [Alias("equip", "reliquia")]
    [Summary("Equipa um relógio da loja. Uso: equipar <id>")]
    public async Task EquiparAsync([Remainder] string itemInput)
    {
        if (string.IsNullOrWhiteSpace(itemInput))
        {
            await ReplyAsync("❌ Uso: `equipar <id>`. Veja os relógios com `loja`.");
            return;
        }

        var (success, message) = await _shop.EquipAsync(
            Context.User.Id, Context.User.Username, itemInput);

        await ReplyAsync(success ? $"✅ {message}" : message!);
    }

    [Command("desequipar")]
    [Alias("unequip")]
    [Summary("Desequipa o relógio atual. Uso: desequipar <id>")]
    public async Task DesequiparAsync([Remainder] string itemInput)
    {
        if (string.IsNullOrWhiteSpace(itemInput))
        {
            await ReplyAsync("❌ Uso: `desequipar <id>`.");
            return;
        }

        var (success, message) = await _shop.UnequipAsync(Context.User.Id, itemInput);

        await ReplyAsync(success ? $"✅ {message}" : message!);
    }

    [Command("loja reload")]
    [Alias("shop reload")]
    [Summary("Recarrega o catálogo da loja (somente admin)")]
    public async Task LojaReloadAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        _shop.ForceReload();
        await ReplyAsync("🔄 Catálogo da loja recarregado do banco de dados.");
    }

    [Command("loja add")]
    [Alias("shop add")]
    [Summary("Adiciona um item ao catálogo. Uso: loja add <id> <nome> <emoji> <preço> <categoria> (somente admin)")]
    public async Task LojaAddAsync(
        [Remainder] string input)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            await ReplyAsync("❌ Uso: `loja add <id> <nome> <emoji> <preço> <categoria>`\nCategorias: `cosmetico`, `boost`, `ativo`, `reliquia`.");
            return;
        }

        var key = parts[0].Trim().ToLowerInvariant();
        var name = parts[1];
        var emoji = parts[2];
        if (!ulong.TryParse(parts[3], out var price))
        {
            await ReplyAsync("❌ Preço inválido.");
            return;
        }

        var category = ParseCategory(parts[4]);

        if (category == null)
        {
            await ReplyAsync("❌ Categoria inválida. Use: `cosmetico`, `boost`, `ativo` ou `reliquia`.");
            return;
        }

        ItemCategory cat = category.Value;

        if (await _shop.FindItemAsync(key) != null)
        {
            await ReplyAsync($"❌ Já existe um item com o id `{key}`.");
            return;
        }

        var item = new ShopItem
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = $"Item adicionado por admin.",
            Price = price,
            Category = cat,
            Effect = BoostEffect.None,
            DurationHours = 0,
            DailyIncome = 0,
            MaxQuantity = cat == ItemCategory.Asset || cat == ItemCategory.Relic ? 1 : 0,
            IsActive = true,
            IsPlaceholder = false,
            SortOrder = 100,
            RelicEffect = RelicEffect.None,
            RelicGame = RelicGameType.All,
            RelicValue = 0
        };

        await _shop.UpsertItemAsync(item);
        await ReplyAsync($"✅ Item **{emoji} {name}** (`{key}`) adicionado ao catálogo por **{EconomyFormat.Full(price)}** moedas.");
    }

    private static ItemCategory? ParseCategory(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "cosmetico" or "cosmetic" => ItemCategory.Cosmetic,
            "boost" => ItemCategory.Boost,
            "ativo" or "asset" => ItemCategory.Asset,
            "reliquia" or "relic" => ItemCategory.Relic,
            _ => null
        };
    }

    [Command("loja remover")]
    [Alias("shop remove")]
    [Summary("Remove um item do catálogo. Uso: loja remover <id> (somente admin)")]
    public async Task LojaRemoveAsync([Remainder] string keyOrName)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var item = await _shop.FindItemAsync(keyOrName);
        if (item == null)
        {
            await ReplyAsync("❌ Item não encontrado.");
            return;
        }

        var removed = await _shop.RemoveItemAsync(item.Key);
        if (!removed)
        {
            await ReplyAsync("❌ Falha ao remover o item.");
            return;
        }

        await ReplyAsync($"🗑️ Item **{item.Emoji} {item.Name}** (`{item.Key}`) removido do catálogo.");
    }

    [Command("loja preco")]
    [Alias("shop price")]
    [Summary("Altera o preço de um item. Uso: loja preco <id> <preço> (somente admin)")]
    public async Task LojaPrecoAsync([Remainder] string input)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !ulong.TryParse(parts[1], out var price))
        {
            await ReplyAsync("❌ Uso: `loja preco <id> <preço>`.");
            return;
        }

        var item = await _shop.FindItemAsync(parts[0]);
        if (item == null)
        {
            await ReplyAsync("❌ Item não encontrado.");
            return;
        }

        var previous = item.Price;
        item.Price = price;
        await _shop.UpsertItemAsync(item);
        await ReplyAsync($"💰 **{item.Emoji} {item.Name}**: preço alterado de **{EconomyFormat.Full(previous)}** para **{EconomyFormat.Full(price)}** moedas.");
    }

    [Command("loja ativar")]
    [Alias("loja desativar", "shop activate", "shop deactivate")]
    [Summary("Ativa/desativa um item do catálogo. Uso: loja ativar|desativar <id> (somente admin)")]
    public async Task LojaToggleAsync([Remainder] string keyOrName)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var item = await _shop.FindItemAsync(keyOrName);
        if (item == null)
        {
            await ReplyAsync("❌ Item não encontrado.");
            return;
        }

        var tokens = Context.Message.Content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var raw = tokens.FirstOrDefault(t =>
            t.Equals("ativar", StringComparison.OrdinalIgnoreCase)
            || t.Equals("desativar", StringComparison.OrdinalIgnoreCase)
            || t.Equals("activate", StringComparison.OrdinalIgnoreCase)
            || t.Equals("deactivate", StringComparison.OrdinalIgnoreCase)) ?? "ativar";
        var activate = raw.Equals("ativar", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("activate", StringComparison.OrdinalIgnoreCase);

        item.IsActive = activate;
        await _shop.UpsertItemAsync(item);

        await ReplyAsync(activate
            ? $"✅ **{item.Emoji} {item.Name}** está **ativo** na loja novamente."
            : $"🚫 **{item.Emoji} {item.Name}** foi **desativado** da loja.");
    }
}
