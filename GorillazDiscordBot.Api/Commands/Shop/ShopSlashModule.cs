using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Shop;

public class ShopSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ShopService _shop;

    public ShopSlashModule(ShopService shop)
    {
        _shop = shop;
    }

    [SlashCommand("loja", "Mostra os itens disponíveis na loja")]
    public async Task LojaAsync(
        [Summary("categoria", "Filtrar por categoria (padrão: todas)")] ShopCategoryChoice? categoria = null)
    {
        var items = await _shop.GetCatalogAsync();
        var active = items.Where(i => i.IsActive).ToList();
        var real = active.Where(i => !i.IsPlaceholder).ToList();
        var placeholders = active.Where(i => i.IsPlaceholder).ToList();

        if (real.Count == 0 && placeholders.Count == 0)
        {
            await RespondAsync("🛒 A loja está vazia no momento.");
            return;
        }

        if (categoria is { } cat && cat != ShopCategoryChoice.Todas)
        {
            var itemCategory = cat switch
            {
                ShopCategoryChoice.Colecionaves => ItemCategory.Cosmetic,
                ShopCategoryChoice.Boosts => ItemCategory.Boost,
                ShopCategoryChoice.Ativos => ItemCategory.Asset,
                ShopCategoryChoice.Relogios => ItemCategory.Relic,
                ShopCategoryChoice.Pets => ItemCategory.Pet,
                _ => (ItemCategory?)null
            };

            if (itemCategory is null)
            {
                await RespondAsync("❌ Categoria inválida.");
                return;
            }

            var section = real.Where(i => i.Category == itemCategory.Value).OrderByDescending(i => i.Price).ToList();

            if (section.Count == 0)
            {
                var sectionPlaceholders = placeholders.Where(i => i.Category == itemCategory.Value).ToList();
                if (sectionPlaceholders.Count == 0)
                {
                    await RespondAsync($"🛒 Nenhum item disponível na categoria **{GetCategoryTitle(itemCategory.Value)}** no momento.");
                    return;
                }
            }

            var embed = BuildCategorySectionEmbed(itemCategory.Value, section);
            var components = BuildShopCategoryComponents(Context.User.Id, itemCategory.Value, section);
            await RespondAsync(embed: embed, components: components);
            return;
        }

        var overviewEmbed = BuildShopOverviewEmbed(Context.User, real, placeholders);
        var categoryComponents = BuildShopCategorySelectMenu(Context.User.Id, real, placeholders);
        await RespondAsync(embed: overviewEmbed, components: categoryComponents);
    }

    [SlashCommand("comprar", "Compra um item da loja")]
    public async Task ComprarAsync(
        [Summary("item", "ID ou nome do item")] string item)
    {
        var shopItem = await _shop.FindItemAsync(item);
        if (shopItem == null || !shopItem.IsActive)
        {
            await RespondAsync("❌ Item não encontrado. Use `/loja` para ver os ids disponíveis.", ephemeral: true);
            return;
        }

        var (success, message, _) = await _shop.BuyAsync(
            Context.User.Id, Context.User.Username, shopItem);

        if (!success)
        {
            await RespondAsync(message!, ephemeral: true);
            return;
        }

        await RespondAsync($"✅ **{Context.User.GetDisplayName()}** comprou **{shopItem.Emoji} {shopItem.Name}** por **{EconomyFormat.Full(shopItem.Price)}** moedas!");
    }

    [SlashCommand("vender", "Revende um item por reembolso parcial (50%)")]
    public async Task VenderAsync(
        [Summary("item", "ID ou nome do item")] string item)
    {
        var shopItem = await _shop.FindItemAsync(item);
        if (shopItem == null)
        {
            await RespondAsync("❌ Item não encontrado. Use `/loja` para ver os ids.", ephemeral: true);
            return;
        }

        var (success, message, _) = await _shop.SellAsync(
            Context.User.Id, Context.User.Username, shopItem);

        if (!success)
        {
            await RespondAsync(message!, ephemeral: true);
            return;
        }

        var refund = (ulong)Math.Floor(shopItem.Price * ShopService.SellRefundRate);
        await RespondAsync($"💸 **{Context.User.GetDisplayName()}** vendeu **{shopItem.Emoji} {shopItem.Name}** e recebeu **{EconomyFormat.Full(refund)}** moedas de volta!");
    }

    [SlashCommand("usar", "Ativa um boost comprado")]
    public async Task UsarAsync(
        [Summary("item", "ID ou nome do item")] string item)
    {
        var shopItem = await _shop.FindItemAsync(item);
        if (shopItem == null)
        {
            await RespondAsync("❌ Item não encontrado. Use `/loja` para ver os ids.", ephemeral: true);
            return;
        }

        var (success, message, _) = await _shop.UseAsync(
            Context.User.Id, Context.User.Username, shopItem);

        if (!success)
        {
            await RespondAsync(message!, ephemeral: true);
            return;
        }

        await RespondAsync(shopItem.Effect switch
        {
            BoostEffect.DailyX2 => $"⚡ **{Context.User.GetDisplayName()}** ativou **{shopItem.Name}**! Seu **próximo daily** renderá o DOBRO!",
            BoostEffect.WorkX2 => $"💼 **{Context.User.GetDisplayName()}** ativou **{shopItem.Name}**! Seu **próximo trabalho** renderá o DOBRO!",
            BoostEffect.RobShield => $"🛡️ **{Context.User.GetDisplayName()}** ativou o **{shopItem.Name}** por **{shopItem.DurationHours}h**!",
            _ => $"✅ Item usado!"
        });
    }

    private const string InvPrefix = "inv";
    private const string ShopPrefix = "shop";

    [SlashCommand("inventario", "Mostra seus itens com opções de interagir")]
    public async Task InventarioAsync()
    {
        var ownership = await _shop.GetInventoryAsync(Context.User.Id);
        var catalog = await _shop.GetCatalogAsync();

        if (ownership.Count == 0)
        {
            await RespondAsync($"🎒 **{Context.User.GetDisplayName()}**, seu inventário está vazio. Compre algo com `/loja`!");
            return;
        }

        var embed = BuildInventoryOverviewEmbed(Context.User, ownership, catalog);
        var components = BuildInventorySelectMenu(Context.User.Id, ownership, catalog);

        await RespondAsync(embed: embed, components: components);
    }

    [ComponentInteraction("inv:sel:*", true)]
    public async Task InventorySelectAsync(string invokerId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🎒 Use `/inventario` para abrir seu próprio inventário.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var itemKey = component.Data.Values.FirstOrDefault();

        if (string.IsNullOrEmpty(itemKey))
            return;

        var ownership = await _shop.GetInventoryAsync(Context.User.Id);
        var catalog = await _shop.GetCatalogAsync();

        if (itemKey == "back")
        {
            var overviewEmbed = BuildInventoryOverviewEmbed(Context.User, ownership, catalog);
            var overviewComponents = BuildInventorySelectMenu(Context.User.Id, ownership, catalog);
            await component.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = overviewEmbed;
                m.Components = overviewComponents;
            });
            return;
        }

        var invItem = ownership.FirstOrDefault(i => i.ItemKey == itemKey);
        var shopItem = catalog.FirstOrDefault(c => c.Key == itemKey);

        if (invItem == null)
        {
            await FollowupAsync("❌ Item não encontrado no seu inventário.", ephemeral: true);
            return;
        }

        var embed = BuildItemDetailEmbed(Context.User, invItem, shopItem);
        var components2 = BuildItemActionComponents(Context.User.Id, itemKey, invItem, shopItem);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components2;
        });
    }

    [ComponentInteraction("inv:equip:*:*", true)]
    public async Task InventoryEquipAsync(string invokerId, string itemKey)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🎒 Use `/inventario` para abrir seu próprio inventário.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var ownership = await _shop.GetInventoryAsync(Context.User.Id);
        var invItem = ownership.FirstOrDefault(i => i.ItemKey == itemKey);
        var catalog = await _shop.GetCatalogAsync();
        var shopItem = catalog.FirstOrDefault(c => c.Key == itemKey);

        if (invItem == null)
        {
            await FollowupAsync("❌ Item não encontrado no seu inventário.", ephemeral: true);
            return;
        }

        string message;
        if (invItem.IsEquipped)
        {
            var (success, msg) = await _shop.UnequipAsync(Context.User.Id, itemKey);
            message = success ? $"✅ {msg}" : msg!;
        }
        else
        {
            var (success, msg) = await _shop.EquipAsync(Context.User.Id, Context.User.Username, itemKey);
            message = success ? $"✅ {msg}" : msg!;
        }

        var component = (SocketMessageComponent)Context.Interaction;
        var updatedOwnership = await _shop.GetInventoryAsync(Context.User.Id);
        var updatedInvItem = updatedOwnership.FirstOrDefault(i => i.ItemKey == itemKey);
        var embed = BuildItemDetailEmbed(Context.User, updatedInvItem ?? invItem, shopItem, message);
        var components = BuildItemActionComponents(Context.User.Id, itemKey, updatedInvItem ?? invItem, shopItem);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    [ComponentInteraction("inv:use:*:*", true)]
    public async Task InventoryUseAsync(string invokerId, string itemKey)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🎒 Use `/inventario` para abrir seu próprio inventário.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var catalog = await _shop.GetCatalogAsync();
        var shopItem = catalog.FirstOrDefault(c => c.Key == itemKey);

        if (shopItem == null)
        {
            await FollowupAsync("❌ Item não encontrado.", ephemeral: true);
            return;
        }

        var (success, message, _) = await _shop.UseAsync(
            Context.User.Id, Context.User.Username, shopItem);

        if (!success)
        {
            await FollowupAsync(message!, ephemeral: true);
            return;
        }

        var component = (SocketMessageComponent)Context.Interaction;
        var updatedOwnership = await _shop.GetInventoryAsync(Context.User.Id);
        var updatedInvItem = updatedOwnership.FirstOrDefault(i => i.ItemKey == itemKey);

        if (updatedInvItem == null)
        {
            var emptyEmbed = new EmbedBuilder()
                .WithBlurpleTheme()
                .WithDescription($"✅ {message}\n\n📦 O item foi consumido e removido do inventário.")
                .Build();
            await component.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = emptyEmbed;
                m.Components = new ComponentBuilder().Build();
            });
            return;
        }

        var embed = BuildItemDetailEmbed(Context.User, updatedInvItem, shopItem, message);
        var components = BuildItemActionComponents(Context.User.Id, itemKey, updatedInvItem, shopItem);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    [ComponentInteraction("inv:sell:*:*", true)]
    public async Task InventorySellAsync(string invokerId, string itemKey)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🎒 Use `/inventario` para abrir seu próprio inventário.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var catalog = await _shop.GetCatalogAsync();
        var shopItem = catalog.FirstOrDefault(c => c.Key == itemKey);

        if (shopItem == null)
        {
            await FollowupAsync("❌ Item não encontrado.", ephemeral: true);
            return;
        }

        var (success, message, _) = await _shop.SellAsync(
            Context.User.Id, Context.User.Username, shopItem);

        if (!success)
        {
            await FollowupAsync(message!, ephemeral: true);
            return;
        }

        var component = (SocketMessageComponent)Context.Interaction;
        var updatedOwnership = await _shop.GetInventoryAsync(Context.User.Id);

        if (updatedOwnership.Count == 0)
        {
            var emptyEmbed = new EmbedBuilder()
                .WithBlurpleTheme()
                .WithDescription($"✅ {message}\n\n📦 Seu inventário está vazio agora.")
                .Build();
            await component.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = emptyEmbed;
                m.Components = new ComponentBuilder().Build();
            });
            return;
        }

        var updatedCatalog = await _shop.GetCatalogAsync();
        var embed = BuildInventoryOverviewEmbed(Context.User, updatedOwnership, updatedCatalog);
        var components2 = BuildInventorySelectMenu(Context.User.Id, updatedOwnership, updatedCatalog);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components2;
        });
    }

    [ComponentInteraction("shop:cat:*", true)]
    public async Task ShopCategorySelectAsync(string invokerId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🛒 Use `/loja` para abrir sua própria loja.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var catId = component.Data.Values.FirstOrDefault();

        if (string.IsNullOrEmpty(catId))
            return;

        var category = ParseCategoryId(catId);
        if (category is null)
        {
            await FollowupAsync("❌ Categoria inválida.", ephemeral: true);
            return;
        }

        await ShowShopCategoryAsync(component, owner, category.Value);
    }

    [ComponentInteraction("shop:item:*:*", true)]
    public async Task ShopItemSelectAsync(string invokerId, string catId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🛒 Use `/loja` para abrir sua própria loja.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var itemKey = component.Data.Values.FirstOrDefault();

        if (string.IsNullOrEmpty(itemKey))
            return;

        if (itemKey == "back")
        {
            await ShowShopOverviewAsync(component, owner);
            return;
        }

        var category = ParseCategoryId(catId);
        if (category is null)
        {
            await FollowupAsync("❌ Categoria inválida.", ephemeral: true);
            return;
        }

        var catalog = await _shop.GetCatalogAsync();
        var shopItem = catalog.FirstOrDefault(c =>
            c.Key == itemKey && c.IsActive && !c.IsPlaceholder && c.Category == category.Value);

        if (shopItem == null)
        {
            await FollowupAsync("❌ Item não encontrado. Selecione outro item.", ephemeral: true);
            return;
        }

        await ShowShopItemAsync(component, owner, shopItem);
    }

    [ComponentInteraction("shop:buy:*:*", true)]
    public async Task ShopBuyAsync(string invokerId, string itemKey)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🛒 Use `/loja` para comprar você mesmo.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var catalog = await _shop.GetCatalogAsync();
        var shopItem = catalog.FirstOrDefault(c =>
            c.Key == itemKey && c.IsActive && !c.IsPlaceholder);

        if (shopItem == null)
        {
            await FollowupAsync("❌ Item não encontrado ou indisponível.", ephemeral: true);
            return;
        }

        var (success, message, balance) = await _shop.BuyAsync(owner, Context.User.Username, shopItem);
        var result = success
            ? $"✅ **{Context.User.GetDisplayName()}** comprou **{shopItem.Emoji} {shopItem.Name}**!"
            : message!;

        await ShowShopItemAsync(component, owner, shopItem, result, success ? balance : null);
    }

    [ComponentInteraction("shop:back:*:*", true)]
    public async Task ShopBackAsync(string invokerId, string catId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🛒 Use `/loja` para abrir sua própria loja.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var category = ParseCategoryId(catId);

        if (category is null)
        {
            await ShowShopOverviewAsync(component, owner);
            return;
        }

        await ShowShopCategoryAsync(component, owner, category.Value);
    }

    [SlashCommand("equipar", "Equip um relógio da loja")]
    public async Task EquiparAsync(
        [Summary("item", "ID ou nome do relógio")] string item)
    {
        var (success, message) = await _shop.EquipAsync(
            Context.User.Id, Context.User.Username, item);

        await RespondAsync(success ? $"✅ {message}" : message!, ephemeral: !success);
    }

    [SlashCommand("desequipar", "Desequipa o relógio ativo")]
    public async Task DesequiparAsync(
        [Summary("item", "ID ou nome do relógio")] string item)
    {
        var (success, message) = await _shop.UnequipAsync(Context.User.Id, item);

        await RespondAsync(success ? $"✅ {message}" : message!, ephemeral: !success);
    }

    private async Task ShowShopOverviewAsync(SocketMessageComponent component, ulong ownerId)
    {
        var catalog = await _shop.GetCatalogAsync();
        var active = catalog.Where(i => i.IsActive).ToList();
        var real = active.Where(i => !i.IsPlaceholder).ToList();
        var placeholders = active.Where(i => i.IsPlaceholder).ToList();

        var embed = BuildShopOverviewEmbed(Context.User, real, placeholders);
        var components = BuildShopCategorySelectMenu(ownerId, real, placeholders);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    private async Task ShowShopCategoryAsync(SocketMessageComponent component, ulong ownerId, ItemCategory category)
    {
        var catalog = await _shop.GetCatalogAsync();
        var section = catalog
            .Where(i => i.IsActive && !i.IsPlaceholder && i.Category == category)
            .OrderByDescending(i => i.Price)
            .ToList();

        var embed = BuildCategorySectionEmbed(category, section);
        var components = BuildShopCategoryComponents(ownerId, category, section);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    private async Task ShowShopItemAsync(
        SocketMessageComponent component, ulong ownerId, ShopItem shopItem,
        string? actionMessage = null, ulong? balanceOverride = null)
    {
        var ownership = await _shop.GetInventoryAsync(ownerId);
        var owned = ownership.FirstOrDefault(i => i.ItemKey == shopItem.Key);
        var balance = balanceOverride ?? await _shop.GetBalanceAsync(ownerId, Context.User.Username);

        var embed = BuildShopItemDetailEmbed(Context.User, shopItem, balance, owned, actionMessage);
        var reachedMax = owned != null && shopItem.MaxQuantity > 0 && owned.Quantity >= shopItem.MaxQuantity;
        var components = BuildShopItemComponents(ownerId, shopItem, reachedMax);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    private static Embed BuildCategorySectionEmbed(ItemCategory category, List<ShopItem> items)
    {
        var (icon, title) = GetCategoryMeta(category);

        var sb = new StringBuilder();

        if (items.Count == 0)
            sb.AppendLine("🔒 Em breve.");

        foreach (var item in items)
        {
            var extra = item.DurationHours > 0 ? $" *(dur. {item.DurationHours}h)*" : "";
            sb.AppendLine($"{item.Emoji} **{item.Name}** — **{EconomyFormat.Full(item.Price)}** moedas");
            sb.AppendLine($"   └ {item.Description}{extra}");
            if (item.Category == ItemCategory.Asset && item.DailyIncome > 0)
                sb.AppendLine($"   └ Renda: **+{EconomyFormat.Full(item.DailyIncome)}/dia** no daily · limite: 1 por usuário");
            if (item.Category == ItemCategory.Relic)
                sb.AppendLine($"   └ Efeito: {DescribeRelic(item)}");
            if (item.Category == ItemCategory.Pet)
                sb.AppendLine($"   └ Efeito: {DescribePet(item)}");
            sb.AppendLine($"   └ id: `{item.Key}`");
            sb.AppendLine();
        }

        return new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor("Loja do Gorillaz")
            .WithTitle($"{icon} {title}")
            .WithDescription(sb.ToString())
            .Build();
    }

    private static (string Icon, string Title) GetCategoryMeta(ItemCategory category) => category switch
    {
        ItemCategory.Cosmetic => ("🎁", "Colecionáveis"),
        ItemCategory.Boost => ("⚡", "Boosts"),
        ItemCategory.Asset => ("📈", "Ativos de Renda"),
        ItemCategory.Relic => ("⌚", "Relógios Equipáveis"),
        ItemCategory.Pet => ("🐾", "Pets"),
        _ => ("📋", "Itens")
    };

    private static string GetCategoryId(ItemCategory category) => category switch
    {
        ItemCategory.Cosmetic => "cosmetic",
        ItemCategory.Boost => "boost",
        ItemCategory.Asset => "asset",
        ItemCategory.Relic => "relic",
        ItemCategory.Pet => "pet",
        _ => "other"
    };

    private static ItemCategory? ParseCategoryId(string id) => id switch
    {
        "cosmetic" => ItemCategory.Cosmetic,
        "boost" => ItemCategory.Boost,
        "asset" => ItemCategory.Asset,
        "relic" => ItemCategory.Relic,
        "pet" => ItemCategory.Pet,
        _ => null
    };

    private static readonly ItemCategory[] ShopCategories =
    {
        ItemCategory.Cosmetic,
        ItemCategory.Boost,
        ItemCategory.Asset,
        ItemCategory.Relic,
        ItemCategory.Pet,
    };

    private static Embed BuildShopOverviewEmbed(IUser user, List<ShopItem> real, List<ShopItem> placeholders)
    {
        var sb = new StringBuilder();

        foreach (var category in ShopCategories)
        {
            var section = real.Where(i => i.Category == category).OrderByDescending(i => i.Price).ToList();
            if (section.Count == 0) continue;

            var (icon, title) = GetCategoryMeta(category);
            var cheapest = section.MinBy(i => i.Price)!;
            sb.AppendLine($"{icon} **{title}** — {section.Count} produto(s)");
            sb.AppendLine($"   └ {cheapest.Emoji} **{cheapest.Name}** a partir de **{EconomyFormat.Full(cheapest.Price)}** moedas");
            sb.AppendLine();
        }

        var placeholderCount = placeholders.Count;
        if (placeholderCount > 0)
            sb.AppendLine($"🔒 **Em breve** — {placeholderCount} item(ns)");

        sb.AppendLine("Selecione uma categoria abaixo ou use `/comprar <id>` para comprar rápido.");

        return new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor("Loja do Gorillaz")
            .WithDescription(sb.ToString())
            .WithStandardFooter("🛒 Escolha uma categoria")
            .Build();
    }

    private static MessageComponent BuildShopCategorySelectMenu(
        ulong userId, List<ShopItem> real, List<ShopItem> placeholders)
    {
        var options = new List<SelectMenuOptionBuilder>();

        foreach (var category in ShopCategories)
        {
            var hasReal = real.Any(i => i.Category == category);
            var hasPlaceholder = placeholders.Any(i => i.Category == category);
            if (!hasReal && !hasPlaceholder) continue;

            var (icon, title) = GetCategoryMeta(category);
            options.Add(new SelectMenuOptionBuilder($"{icon} {title}", GetCategoryId(category)));
        }

        if (options.Count == 0)
            options.Add(new SelectMenuOptionBuilder("📋 Categorias", "back"));

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{ShopPrefix}:cat:{userId}")
                .WithPlaceholder("Escolha uma categoria…")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildShopCategoryComponents(
        ulong userId, ItemCategory category, List<ShopItem> items)
    {
        var options = new List<SelectMenuOptionBuilder> { new("📋 Voltar às categorias", "back") };
        options.AddRange(items.Take(24).Select(i => new SelectMenuOptionBuilder(
            $"{i.Emoji} {i.Name} — {EconomyFormat.Compact(i.Price)}", i.Key)));

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{ShopPrefix}:item:{userId}:{GetCategoryId(category)}")
                .WithPlaceholder("Selecione um item…")
                .WithOptions(options))
            .Build();
    }

    private static Embed BuildShopItemDetailEmbed(
        IUser user, ShopItem item, ulong balance, InventoryItem? owned, string? actionMessage = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(actionMessage))
            sb.AppendLine($"{actionMessage}\n");

        sb.AppendLine($"{item.Emoji} **{item.Name}**");
        if (item.DurationHours > 0)
            sb.AppendLine($"⏰ Duração: **{item.DurationHours}h**");
        if (item.Category == ItemCategory.Asset && item.DailyIncome > 0)
            sb.AppendLine($"📈 Renda: **+{EconomyFormat.Full(item.DailyIncome)}/dia** no daily · limite: 1 por usuário");
        if (item.Category == ItemCategory.Relic)
            sb.AppendLine($"⚙️ Efeito: {DescribeRelic(item)}");
        if (item.Category == ItemCategory.Pet)
            sb.AppendLine($"🐾 Efeito: {DescribePet(item)}");
        if (owned != null)
            sb.AppendLine($"🎒 Você possui: **{owned.Quantity}**");

        sb.AppendLine($"\n{item.Description}");
        sb.AppendLine($"\n💰 Preço: **{EconomyFormat.Full(item.Price)}** moedas");
        sb.AppendLine($"💵 Seu saldo: **{EconomyFormat.Full(balance)}**");

        return new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{user.GetDisplayName()} — Loja", user.GetAvatarUrl())
            .WithTitle($"{item.Emoji} {item.Name}")
            .WithDescription(sb.ToString())
            .WithStandardFooter("🛒 Compre com o botão abaixo")
            .Build();
    }

    private static MessageComponent BuildShopItemComponents(ulong userId, ShopItem item, bool buyDisabled)
    {
        return new ComponentBuilder()
            .WithButton(new ButtonBuilder()
                .WithLabel($"Comprar ({EconomyFormat.Compact(item.Price)})")
                .WithCustomId($"{ShopPrefix}:buy:{userId}:{item.Key}")
                .WithStyle(ButtonStyle.Success)
                .WithEmote(new Emoji("🛒"))
                .WithDisabled(buyDisabled))
            .WithButton(new ButtonBuilder()
                .WithLabel("Voltar")
                .WithCustomId($"{ShopPrefix}:back:{userId}:{GetCategoryId(item.Category)}")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("⬅")))
            .Build();
    }

    private static string GetCategoryTitle(ItemCategory category) => category switch
    {
        ItemCategory.Cosmetic => "Colecionáveis",
        ItemCategory.Boost => "Boosts",
        ItemCategory.Asset => "Ativos de Renda",
        ItemCategory.Relic => "Relógios Equipáveis",
        ItemCategory.Pet => "Pets",
        _ => "Itens"
    };

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

    private static string DescribePet(ShopItem item)
    {
        var target = item.UpgradeEffect switch
        {
            UpgradeEffect.Daily => "no daily",
            UpgradeEffect.Work => "no trabalho",
            UpgradeEffect.Rob => "nos roubos",
            UpgradeEffect.AssetIncome => "na renda dos ativos",
            UpgradeEffect.Savings => "nos juros da poupança",
            _ => "?"
        };
        var max = item.MaxQuantity > 0 ? $" · máx. nível {item.MaxQuantity}" : "";
        return $"🐾 **+{item.UpgradeValue}%** {target} por nível{max}";
    }

    private static Embed BuildInventoryOverviewEmbed(
        IUser user, List<InventoryItem> ownership, List<ShopItem> catalog)
    {
        var sb = new StringBuilder();

        foreach (var invItem in ownership)
        {
            var shopItem = catalog.FirstOrDefault(c => c.Key == invItem.ItemKey);
            var name = shopItem?.Name ?? invItem.ItemKey;
            var emoji = shopItem?.Emoji ?? "❔";
            var qty = invItem.Quantity;
            var validade = invItem.ExpiresAt is { } exp && exp > DateTime.UtcNow
                ? $" *(expira {exp:dd/MM HH:mm} UTC)*"
                : "";

            var linha = $"{emoji} **{name}** × **{qty}**{validade}";

            if (invItem.IsEquipped)
                linha += " · ⭐ **equipado**";

            if (shopItem is { Category: ItemCategory.Asset, DailyIncome: > 0 })
            {
                var baseline = invItem.LastCollectedAt ?? invItem.AcquiredAt ?? DateTime.UtcNow;
                var dias = Math.Clamp((int)Math.Floor((DateTime.UtcNow - baseline).TotalDays), 0, ShopService.MaxIncomeBacklogDays);
                linha += $" · **+{EconomyFormat.Full(shopItem.DailyIncome)}/dia** · {dias} dia(s) acumulados";
            }

            if (shopItem is { Category: ItemCategory.Pet, UpgradeEffect: not UpgradeEffect.None })
                linha += $" · 🐾 nível **{qty}** · {DescribePet(shopItem)}";

            sb.AppendLine(linha);
        }

        return new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{user.GetDisplayName()} — Inventário", user.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithStandardFooter("Selecione um item para interagir")
            .Build();
    }

    private static MessageComponent BuildInventorySelectMenu(
        ulong userId, List<InventoryItem> ownership, List<ShopItem> catalog)
    {
        var options = ownership.Take(25).Select(invItem =>
        {
            var shopItem = catalog.FirstOrDefault(c => c.Key == invItem.ItemKey);
            var name = shopItem?.Name ?? invItem.ItemKey;
            var emoji = shopItem?.Emoji ?? "❔";
            var label = invItem.IsEquipped ? $"{emoji} {name} ⭐" : $"{emoji} {name}";
            return new SelectMenuOptionBuilder(label, invItem.ItemKey);
        }).ToList();

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{InvPrefix}:sel:{userId}")
                .WithPlaceholder("Selecione um item…")
                .WithOptions(options))
            .Build();
    }

    private static Embed BuildItemDetailEmbed(
        IUser user, InventoryItem invItem, ShopItem? shopItem, string? actionMessage = null)
    {
        var name = shopItem?.Name ?? invItem.ItemKey;
        var emoji = shopItem?.Emoji ?? "❔";
        var description = shopItem?.Description ?? "Sem descrição.";

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(actionMessage))
            sb.AppendLine($"{actionMessage}\n");

        sb.AppendLine($"{emoji} **{name}** × **{invItem.Quantity}**");
        if (invItem.IsEquipped)
            sb.AppendLine("⭐ **Equipado**");
        if (invItem.ExpiresAt is { } exp && exp > DateTime.UtcNow)
            sb.AppendLine($"⏰ Expira em: **{exp:dd/MM/yyyy HH:mm} UTC**");
        if (shopItem is { Category: ItemCategory.Asset, DailyIncome: > 0 })
        {
            var baseline = invItem.LastCollectedAt ?? invItem.AcquiredAt ?? DateTime.UtcNow;
            var dias = Math.Clamp((int)Math.Floor((DateTime.UtcNow - baseline).TotalDays), 0, ShopService.MaxIncomeBacklogDays);
            sb.AppendLine($"📈 Renda: **+{EconomyFormat.Full(shopItem.DailyIncome)}/dia** · {dias} dia(s) acumulados");
        }
        if (shopItem?.Category == ItemCategory.Relic)
            sb.AppendLine($"⚙️ Efeito: {DescribeRelic(shopItem)}");
        if (shopItem is { Category: ItemCategory.Pet, UpgradeEffect: not UpgradeEffect.None })
            sb.AppendLine($"🐾 Nível **{invItem.Quantity}** · {DescribePet(shopItem)}");

        sb.AppendLine($"\n{description}");

        var categoryTitle = shopItem != null ? GetCategoryTitle(shopItem.Category) : "Desconhecido";

        return new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{user.GetDisplayName()} — Inventário", user.GetAvatarUrl())
            .WithTitle($"{emoji} {name}")
            .WithDescription(sb.ToString())
            .WithFooter($"Categoria: {categoryTitle}")
            .Build();
    }

    private static MessageComponent BuildItemActionComponents(
        ulong userId, string itemKey, InventoryItem invItem, ShopItem? shopItem)
    {
        var builder = new ComponentBuilder();
        var actionRow = 0;

        if (shopItem?.Category == ItemCategory.Relic)
        {
            var label = invItem.IsEquipped ? "Desequipar" : "Equipar";
            builder.WithButton(new ButtonBuilder()
                .WithLabel(label)
                .WithCustomId($"{InvPrefix}:equip:{userId}:{itemKey}")
                .WithStyle(invItem.IsEquipped ? ButtonStyle.Secondary : ButtonStyle.Primary)
                .WithEmote(new Emoji(invItem.IsEquipped ? "📤" : "📥")), actionRow);
        }
        else if (shopItem?.Category == ItemCategory.Boost)
        {
            builder.WithButton(new ButtonBuilder()
                .WithLabel("Usar")
                .WithCustomId($"{InvPrefix}:use:{userId}:{itemKey}")
                .WithStyle(ButtonStyle.Success)
                .WithEmote(new Emoji("⚡")), actionRow);
        }

        var refund = shopItem != null
            ? (ulong)Math.Floor(shopItem.Price * ShopService.SellRefundRate)
            : 0;
        builder.WithButton(new ButtonBuilder()
            .WithLabel($"Vender ({EconomyFormat.Compact(refund)})")
            .WithCustomId($"{InvPrefix}:sell:{userId}:{itemKey}")
            .WithStyle(ButtonStyle.Danger)
            .WithEmote(new Emoji("💸")), actionRow);

        builder.WithSelectMenu(new SelectMenuBuilder()
            .WithCustomId($"{InvPrefix}:sel:{userId}")
            .WithPlaceholder("Voltar ao inventário…")
            .AddOption("📋 Ver inventário", "back"), actionRow + 1);

        return builder.Build();
    }
}

public enum ShopCategoryChoice
{
    [ChoiceDisplay("Todas")]
    Todas,
    [ChoiceDisplay("🎁 Colecionáveis")]
    Colecionaves,
    [ChoiceDisplay("⚡ Boosts")]
    Boosts,
    [ChoiceDisplay("📈 Ativos de Renda")]
    Ativos,
    [ChoiceDisplay("⌚ Relógios Equipáveis")]
    Relogios,
    [ChoiceDisplay("🐾 Pets")]
    Pets
}
