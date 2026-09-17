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
    private const string SelectAction = "sel";
    private const string ParkAction = "park";

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

    [SlashCommand("garagem", "Mostra os veículos que você possui e permite dirigir")]
    public async Task GaragemAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var (currentKey, ownedVehicles) = await BuildGarageStateAsync(mainId);

        await RespondAsync(
            embed: BuildGarageEmbed(Context.User, profile, ownedVehicles, currentKey),
            components: BuildGarageComponents(Context.User.Id, ownedVehicles, currentKey));
    }

    [ComponentInteraction(GaragePrefix + ":" + SelectAction + ":*", true)]
    public async Task SelectAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta garagem não é sua.", ephemeral: true);
            return;
        }

        var values = ((SocketMessageComponent)Context.Interaction).Data.Values;
        var itemKey = values.FirstOrDefault();
        if (itemKey == null)
        {
            await FollowupAsync("❌ Seleção inválida.", ephemeral: true);
            return;
        }

        var (success, message) = await _shop.DriveVehicleAsync(
            Context.User.Id, Context.User.Username, itemKey);

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var (currentKey, ownedVehicles) = await BuildGarageStateAsync(mainId);

        var sb = new StringBuilder();
        if (!success)
            sb.AppendLine($"{message}\n");

        var embed = BuildGarageEmbed(Context.User, profile, ownedVehicles, currentKey, sb.ToString());
        var components = BuildGarageComponents(Context.User.Id, ownedVehicles, currentKey);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    [ComponentInteraction(GaragePrefix + ":" + ParkAction + ":*", true)]
    public async Task ParkAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta garagem não é sua.", ephemeral: true);
            return;
        }

        var (success, message) = await _shop.ParkVehicleAsync(Context.User.Id);

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var (currentKey, ownedVehicles) = await BuildGarageStateAsync(mainId);

        var sb = new StringBuilder();
        if (!success)
            sb.AppendLine($"{message}\n");

        var embed = BuildGarageEmbed(Context.User, profile, ownedVehicles, currentKey, sb.ToString());
        var components = BuildGarageComponents(Context.User.Id, ownedVehicles, currentKey);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    private async Task<(string? CurrentKey, List<(InventoryItem Inv, ShopItem Item)> Vehicles)> BuildGarageStateAsync(
        ulong mainId)
    {
        var current = await _shop.GetCurrentVehicleAsync(mainId);
        var ownership = await _shop.GetInventoryAsync(mainId);
        var catalog = await _shop.GetCatalogAsync();

        var vehicles = ownership
            .Where(inv => inv.Quantity > 0)
            .Select(inv => (Inv: inv, Item: catalog.FirstOrDefault(c => c.Key == inv.ItemKey)))
            .Where(pair => pair.Item is { Category: ItemCategory.Vehicle })
            .Select(pair => (pair.Inv, pair.Item!))
            .ToList();

        return (current.key, vehicles);
    }

    private static Embed BuildGarageEmbed(
        IUser user, CharacterProfile profile, List<(InventoryItem Inv, ShopItem Item)> vehicles,
        string? currentKey, string? notice = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(notice))
            sb.AppendLine(notice);

        if (vehicles.Count == 0)
        {
            sb.AppendLine("🏍️ Sua garagem está **vazia**. Compre um veículo na `/loja`!");
        }
        else
        {
            sb.AppendLine($"**{vehicles.Count} veículo(s) na garagem**");
            sb.AppendLine();
            foreach (var (_, item) in vehicles)
            {
                var required = VehicleRules.RequiredLicense(item);
                var licenseStatus = required is { } lic
                    ? (profile.Licencas.Contains(lic)
                        ? "🟢 liberado"
                        : $"🔒 falta {VehicleRules.FormatRequirement(lic)}")
                    : "🟢 liberado";

                var driving = currentKey == item.Key ? " ⭐ **dirigindo**" : "";
                sb.AppendLine($"{item.Emoji} **{item.Name}** · {licenseStatus}{driving}");
            }
        }

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Garagem", user.GetAvatarUrl())
            .WithTitle("🚗 Sua garagem")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Use o menu para dirigir um veículo")
            .Build();
    }

    private static MessageComponent BuildGarageComponents(
        ulong ownerId, List<(InventoryItem Inv, ShopItem Item)> vehicles, string? currentKey)
    {
        var builder = new ComponentBuilder();

        if (vehicles.Count > 0)
        {
            var options = vehicles.Select(v => new SelectMenuOptionBuilder(
                    $"{v.Item.Emoji} {v.Item.Name}",
                    v.Item.Key))
                .ToList();

            builder.WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{GaragePrefix}:{SelectAction}:{ownerId}")
                .WithPlaceholder("Escolha um veículo para dirigir…")
                .WithOptions(options));
        }

        if (currentKey != null)
        {
            builder.WithButton("🅿️ Estacionar", $"{GaragePrefix}:{ParkAction}:{ownerId}", ButtonStyle.Secondary);
        }

        return builder.Build();
    }
}