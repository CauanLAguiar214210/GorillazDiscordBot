using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Vehicle;

public class GaragemSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string GaragePrefix = "gar";
    private const string DomainAction  = "dom";
    private const string VehicleAction = "veh";
    private const string BackAction    = "back";
    private const string DriveAction   = "drive";
    private const string ParkAction    = "park";
    private const string SellAction    = "sell";
    private const string ConfirmSell   = "sellok";

    private readonly ShopService _shop;
    private readonly ICharacterProfileRepository _profiles;
    private readonly IEconomyAccessor _accessor;

    public GaragemSlashModule(
        ShopService shop,
        ICharacterProfileRepository profiles,
        IEconomyAccessor accessor)
    {
        _shop = shop;
        _profiles = profiles;
        _accessor = accessor;
    }

    // ──────────────────────────────────────────────────────────────
    // /garagem — overview com subcategorias
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("garagem", "Mostra os veículos que você possui (garagem, marina e hangar)")]
    public async Task GaragemAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var state   = await BuildStateAsync(mainId);

        await RespondAsync(
            embed:      BuildOverviewEmbed(Context.User, state),
            components: BuildDomainSelectMenu(Context.User.Id, state));
    }

    // ──────────────────────────────────────────────────────────────
    // Seleção de domínio (Garagem / Marina / Hangar)
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + DomainAction + ":*", true)]
    public async Task DomainSelectAsync(ulong ownerId)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var value = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(value)) return;

        if (value == "back")
        {
            await ShowOverviewAsync(component, ownerId);
            return;
        }

        if (!Enum.TryParse<LicenseDomain>(value, ignoreCase: true, out var domain))
        {
            await FollowupAsync("Domínio inválido.", ephemeral: true);
            return;
        }

        await ShowDomainListAsync(component, ownerId, domain);
    }

    // ──────────────────────────────────────────────────────────────
    // Seleção de veículo dentro de um domínio
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + VehicleAction + ":*:*", true)]
    public async Task VehicleSelectAsync(ulong ownerId, string domainId)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var itemKey = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(itemKey)) return;

        if (itemKey == "back")
        {
            if (Enum.TryParse<LicenseDomain>(domainId, ignoreCase: true, out var dom))
                await ShowDomainListAsync(component, ownerId, dom);
            else
                await ShowOverviewAsync(component, ownerId);
            return;
        }

        await ShowVehicleDetailAsync(component, ownerId, itemKey, domainId);
    }

    // ──────────────────────────────────────────────────────────────
    // Botão Dirigir
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + DriveAction + ":*:*", true)]
    public async Task DriveAsync(ulong ownerId, string itemKey)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component  = (SocketMessageComponent)Context.Interaction;
        var (success, message) = await _shop.DriveVehicleAsync(Context.User.Id, Context.User.Username, itemKey);

        await ShowVehicleDetailAsync(component, ownerId, itemKey, null, success ? null : message);
    }

    // ──────────────────────────────────────────────────────────────
    // Botão Estacionar
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + ParkAction + ":*:*", true)]
    public async Task ParkAsync(ulong ownerId, string itemKey)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        await _shop.ParkVehicleAsync(Context.User.Id);

        await ShowVehicleDetailAsync(component, ownerId, itemKey, null);
    }

    // ──────────────────────────────────────────────────────────────
    // Botão Voltar (detalhe → lista do domínio)
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + BackAction + ":*:*", true)]
    public async Task BackAsync(ulong ownerId, string domainId)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;

        if (Enum.TryParse<LicenseDomain>(domainId, ignoreCase: true, out var domain))
            await ShowDomainListAsync(component, ownerId, domain);
        else
            await ShowOverviewAsync(component, ownerId);
    }

    // ──────────────────────────────────────────────────────────────
    // Botão Vender (confirmação)
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + SellAction + ":*:*", true)]
    public async Task SellPromptAsync(ulong ownerId, string itemKey)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var catalog   = await _shop.GetCatalogAsync();
        var item      = catalog.FirstOrDefault(i => i.Key == itemKey);

        if (item == null)
        {
            await FollowupAsync("Veículo não encontrado.", ephemeral: true);
            return;
        }

        var refund = (ulong)Math.Floor(item.Price * ShopService.SellRefundRate);

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Vender Veículo", Context.User.GetAvatarUrl())
            .WithTitle($"{item.Emoji} Vender {item.Name}?")
            .WithDescription(
                $"Tem certeza que quer vender **{item.Emoji} {item.Name}**?\n\n" +
                $"Você receberá **{EconomyFormat.Full(refund)} moedas** (50% do valor original de {EconomyFormat.Full(item.Price)}).\n\n" +
                "⚠️ Esta ação é irreversível.")
            .WithStandardFooter("Confirme abaixo")
            .Build();

        var components = new ComponentBuilder()
            .WithButton("✅ Confirmar venda", $"{GaragePrefix}:{ConfirmSell}:{ownerId}:{itemKey}", ButtonStyle.Danger)
            .WithButton("❌ Cancelar",        $"{GaragePrefix}:{BackAction}:{ownerId}:{DomainOf(item).ToString().ToLowerInvariant()}", ButtonStyle.Secondary)
            .Build();

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Botão Confirmar venda
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(GaragePrefix + ":" + ConfirmSell + ":*:*", true)]
    public async Task ConfirmSellAsync(ulong ownerId, string itemKey)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta garagem não é sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var catalog   = await _shop.GetCatalogAsync();
        var item      = catalog.FirstOrDefault(i => i.Key == itemKey);

        if (item == null)
        {
            await FollowupAsync("Veículo não encontrado.", ephemeral: true);
            return;
        }

        var (success, _, balance) = await _shop.SellAsync(Context.User.Id, Context.User.Username, item);

        if (!success)
        {
            await ShowVehicleDetailAsync(component, ownerId, itemKey, null, "Não foi possível vender o veículo.");
            return;
        }

        var refund = (ulong)Math.Floor(item.Price * ShopService.SellRefundRate);

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Venda concluída", Context.User.GetAvatarUrl())
            .WithTitle($"{item.Emoji} {item.Name} vendido!")
            .WithDescription(
                $"Você vendeu **{item.Emoji} {item.Name}** e recebeu **{EconomyFormat.Full(refund)} moedas**.\n\n" +
                $"💰 Saldo atual: **{EconomyFormat.Full(balance)}**")
            .Build();

        var mainId  = await _accessor.ResolveMainIdAsync(ownerId);
        var state   = await BuildStateAsync(mainId);
        var hasMore = state.Vehicles.Any();

        var components = hasMore
            ? BuildDomainSelectMenu(ownerId, state)
            : new ComponentBuilder().Build();

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Show helpers
    // ──────────────────────────────────────────────────────────────

    private async Task ShowOverviewAsync(SocketMessageComponent component, ulong ownerId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(ownerId);
        var state  = await BuildStateAsync(mainId);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildOverviewEmbed(Context.User, state);
            m.Components = BuildDomainSelectMenu(ownerId, state);
        });
    }

    private async Task ShowDomainListAsync(SocketMessageComponent component, ulong ownerId, LicenseDomain domain)
    {
        var mainId   = await _accessor.ResolveMainIdAsync(ownerId);
        var state    = await BuildStateAsync(mainId);
        var vehicles = state.Vehicles
            .Where(v => DomainOf(v.Item) == domain)
            .ToList();

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildDomainEmbed(Context.User, domain, vehicles, state.CurrentKey, state.Profile);
            m.Components = BuildVehicleSelectMenu(ownerId, domain, vehicles);
        });
    }

    private async Task ShowVehicleDetailAsync(
        SocketMessageComponent component, ulong ownerId,
        string itemKey, string? domainId, string? notice = null)
    {
        var mainId  = await _accessor.ResolveMainIdAsync(ownerId);
        var state   = await BuildStateAsync(mainId);
        var catalog = await _shop.GetCatalogAsync();
        var item    = catalog.FirstOrDefault(i => i.Key == itemKey);

        if (item == null)
        {
            await ShowOverviewAsync(component, ownerId);
            return;
        }

        var domain = domainId is { } d && Enum.TryParse<LicenseDomain>(d, ignoreCase: true, out var parsed)
            ? parsed
            : DomainOf(item);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildVehicleDetailEmbed(Context.User, item, state, notice);
            m.Components = BuildVehicleDetailComponents(ownerId, item, state.CurrentKey, domain);
        });
    }

    // ──────────────────────────────────────────────────────────────
    // State builder
    // ──────────────────────────────────────────────────────────────

    private async Task<GarageState> BuildStateAsync(ulong mainId)
    {
        var profile   = await _profiles.GetOrCreateAsync(mainId, string.Empty);
        var current   = await _shop.GetCurrentVehicleAsync(mainId);
        var ownership = await _shop.GetInventoryAsync(mainId);
        var catalog   = await _shop.GetCatalogAsync();

        var vehicles = ownership
            .Where(inv => inv.Quantity > 0)
            .Select(inv => (Inv: inv, Item: catalog.FirstOrDefault(c => c.Key == inv.ItemKey)))
            .Where(p => p.Item is { Category: ItemCategory.Vehicle })
            .Select(p => (p.Inv, p.Item!))
            .ToList();

        return new GarageState(profile, current.key, vehicles);
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de embed
    // ──────────────────────────────────────────────────────────────

    private static Embed BuildOverviewEmbed(IUser user, GarageState state)
    {
        var sb = new StringBuilder();

        if (state.Vehicles.Count == 0)
        {
            sb.AppendLine("Sua coleção está **vazia**. Compre um veículo na `/loja`!");
        }
        else
        {
            foreach (var domain in AllDomains)
            {
                var group = state.Vehicles.Where(v => DomainOf(v.Item) == domain).ToList();
                if (group.Count == 0) continue;

                var (icon, label) = DomainMeta(domain);
                sb.AppendLine($"{icon} **{label}** — {group.Count} veículo(s)");
                foreach (var (_, item) in group)
                {
                    var driving = state.CurrentKey == item.Key ? " ⭐ **em uso**" : "";
                    sb.AppendLine($"   └ {item.Emoji} {item.Name}{driving}");
                }
                sb.AppendLine();
            }
        }

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Garagem", user.GetAvatarUrl())
            .WithTitle("🚗 Seus Veículos")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Selecione uma categoria abaixo")
            .Build();
    }

    private static Embed BuildDomainEmbed(
        IUser user, LicenseDomain domain,
        List<(InventoryItem Inv, ShopItem Item)> vehicles,
        string? currentKey, CharacterProfile profile)
    {
        var (icon, label) = DomainMeta(domain);
        var sb = new StringBuilder();

        if (vehicles.Count == 0)
        {
            sb.AppendLine($"Você não tem nenhum veículo na **{label}**.");
            sb.AppendLine("Compre um na `/loja`.");
        }
        else
        {
            foreach (var (_, item) in vehicles)
            {
                var req     = VehicleRules.RequiredLicense(item);
                var licOk   = req is null || profile.Licencas.Contains(req.Value);
                var licLine = req is { } lic
                    ? (licOk ? "🟢 licença ok" : $"🔒 falta {VehicleRules.FormatRequirement(lic)}")
                    : "🟢 liberado";

                var driving = currentKey == item.Key ? " ⭐ **em uso**" : "";
                sb.AppendLine($"{item.Emoji} **{item.Name}**{driving}");
                sb.AppendLine($"   └ {licLine}");
                sb.AppendLine();
            }
        }

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — {label}", user.GetAvatarUrl())
            .WithTitle($"{icon} {label}")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Selecione um veículo abaixo")
            .Build();
    }

    private static Embed BuildVehicleDetailEmbed(
        IUser user, ShopItem item, GarageState state, string? notice = null)
    {
        var req     = VehicleRules.RequiredLicense(item);
        var licOk   = req is null || state.Profile.Licencas.Contains(req.Value);
        var driving = state.CurrentKey == item.Key;
        var refund  = (ulong)Math.Floor(item.Price * ShopService.SellRefundRate);

        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(notice))
            sb.AppendLine($"⚠️ {notice}\n");

        sb.AppendLine($"{item.Emoji} **{item.Name}**");
        sb.AppendLine($"   └ {item.Description}");
        sb.AppendLine();

        if (req is { } lic)
            sb.AppendLine(licOk
                ? $"🟢 Licença **{VehicleRules.FormatRequirement(lic)}** — ok"
                : $"🔒 Requer **{VehicleRules.FormatRequirement(lic)}** — use `/veiculo licenca prova`");

        sb.AppendLine(driving ? "⭐ **Em uso no momento**" : "🅿️ Guardado");
        sb.AppendLine();
        sb.AppendLine($"💰 Valor original: **{EconomyFormat.Full(item.Price)}** moedas");
        sb.AppendLine($"💸 Venda (50%): **{EconomyFormat.Full(refund)}** moedas");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — {item.Name}", user.GetAvatarUrl())
            .WithTitle($"{item.Emoji} {item.Name}")
            .WithDescription(sb.ToString())
            .WithStandardFooter("/garagem")
            .Build();
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de componentes
    // ──────────────────────────────────────────────────────────────

    private static MessageComponent BuildDomainSelectMenu(ulong userId, GarageState state)
    {
        var options = new List<SelectMenuOptionBuilder>();

        foreach (var domain in AllDomains)
        {
            var count = state.Vehicles.Count(v => DomainOf(v.Item) == domain);
            if (count == 0) continue;

            var (icon, label) = DomainMeta(domain);
            options.Add(new SelectMenuOptionBuilder($"{icon} {label} ({count})", domain.ToString().ToLowerInvariant()));
        }

        if (options.Count == 0)
            options.Add(new SelectMenuOptionBuilder("🚗 Garagem (vazia)", "terrestre"));

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{GaragePrefix}:{DomainAction}:{userId}")
                .WithPlaceholder("Escolha uma categoria…")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildVehicleSelectMenu(
        ulong userId, LicenseDomain domain,
        List<(InventoryItem Inv, ShopItem Item)> vehicles)
    {
        var options = new List<SelectMenuOptionBuilder>
        {
            new("📋 Voltar", "back")
        };

        options.AddRange(vehicles.Select(v =>
            new SelectMenuOptionBuilder($"{v.Item.Emoji} {v.Item.Name}", v.Item.Key)));

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{GaragePrefix}:{VehicleAction}:{userId}:{domain.ToString().ToLowerInvariant()}")
                .WithPlaceholder("Selecione um veículo…")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildVehicleDetailComponents(
        ulong userId, ShopItem item, string? currentKey, LicenseDomain domain)
    {
        var driving  = currentKey == item.Key;
        var domainId = domain.ToString().ToLowerInvariant();

        var builder = new ComponentBuilder();

        if (driving)
        {
            builder.WithButton("🅿️ Estacionar",
                $"{GaragePrefix}:{ParkAction}:{userId}:{item.Key}",
                ButtonStyle.Secondary);
        }
        else
        {
            builder.WithButton("🚗 Dirigir",
                $"{GaragePrefix}:{DriveAction}:{userId}:{item.Key}",
                ButtonStyle.Primary);
        }

        builder.WithButton("💸 Vender",
            $"{GaragePrefix}:{SellAction}:{userId}:{item.Key}",
            ButtonStyle.Danger);

        builder.WithButton("⬅ Voltar",
            $"{GaragePrefix}:{BackAction}:{userId}:{domainId}",
            ButtonStyle.Secondary);

        return builder.Build();
    }

    // ──────────────────────────────────────────────────────────────
    // Utilitários
    // ──────────────────────────────────────────────────────────────

    private static readonly LicenseDomain[] AllDomains =
    {
        LicenseDomain.Terrestre,
        LicenseDomain.Maritima,
        LicenseDomain.Aerea
    };

    private static LicenseDomain DomainOf(ShopItem item)
    {
        var lic = VehicleRules.RequiredLicense(item);
        if (lic is null) return LicenseDomain.Terrestre;
        return LicenseProgression.Info(lic.Value).Domain;
    }

    private static (string Icon, string Label) DomainMeta(LicenseDomain domain) => domain switch
    {
        LicenseDomain.Terrestre => ("🚗", "Garagem"),
        LicenseDomain.Maritima  => ("⛵", "Marina"),
        LicenseDomain.Aerea     => ("✈️", "Hangar"),
        _                       => ("🚗", "Garagem")
    };
}

/// <summary>Estado da garagem carregado uma vez por interação.</summary>
internal sealed record GarageState(
    CharacterProfile Profile,
    string? CurrentKey,
    List<(InventoryItem Inv, ShopItem Item)> Vehicles);
