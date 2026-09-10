using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Entity.Economy;
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
            ("🧪", "Consumíveis", ItemCategory.Consumable),
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

        var pets = real.Where(i => i.Category == ItemCategory.Pet).OrderByDescending(i => i.Price).ToList();
        if (pets.Count > 0)
        {
            sb.AppendLine("🐾 **Pets** *(bônus permanente — máximo 2 tipos)*");
            foreach (var item in pets)
            {
                sb.AppendLine($"{item.Emoji} **{item.Name}** — **{EconomyFormat.Full(item.Price)}** moedas");
                sb.AppendLine($"   └ {item.Description}");
                sb.AppendLine($"   └ Efeito: {PetFormatter.DescribePet(item)}");
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
            RelicGameType.Dice => "dados",
            RelicGameType.Coin => "cara ou coroa",
            RelicGameType.Aviao => "aviaozinho",
            RelicGameType.VideoPoker => "poker de máquina",
            RelicGameType.Mines => "minas",
            RelicGameType.Limbo => "limbo",
            RelicGameType.Rps => "jokenpô",
            RelicGameType.Race => "corrida",
            RelicGameType.Plinko => "plinko",
            RelicGameType.Wheel => "roda da fortuna",
            RelicGameType.HighLow => "maior/menor",
            RelicGameType.Baccarat => "baccarat",
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

        if (item.Category == ItemCategory.Consumable)
        {
            await ReplyAsync(message ?? $"✅ **{item.Name}** usado!");
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
            var name = pair.Value.PetNickname ?? item?.Name ?? pair.Key;
            var emoji = item?.Emoji ?? "❔";
            var qty = pair.Value.Quantity;
            var validade = pair.Value.ExpiresAt is { } exp && exp > DateTime.UtcNow
                ? $" *(expira {exp:dd/MM HH:mm} UTC)*"
                : "";

            var linha = $"{emoji} **{name}** × **{qty}**{validade}";

            if (pair.Value.IsEquipped)
                linha += " · ⭐ **equipado**";

            if (item is { Category: ItemCategory.Asset, DailyIncome: > 0 })
            {
                var baseline = pair.Value.LastCollectedAt ?? pair.Value.AcquiredAt ?? DateTime.UtcNow;
                var dias = Math.Clamp((int)Math.Floor((DateTime.UtcNow - baseline).TotalDays), 0, ShopService.MaxIncomeBacklogDays);
                linha += $" · **+{EconomyFormat.Full(item.DailyIncome)}/dia** · {dias} dia(s) acumulados";
            }

            if (item is { Category: ItemCategory.Pet })
                linha += $" · {PetFormatter.ProgressLine(item, qty)}";

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
}
