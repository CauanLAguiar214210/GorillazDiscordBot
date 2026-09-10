using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Shop;

public class PetSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string PetsPrefix = "pets";
    private const int MaxPetSlots = 2;

    private readonly ShopService _shop;

    public PetSlashModule(ShopService shop)
    {
        _shop = shop;
    }

    [SlashCommand("pets", "Mostra seus pets e o bônus ativo de cada um")]
    public async Task PetsAsync()
    {
        var (embed, components) = await BuildPetsScreenAsync();
        await RespondAsync(embed: embed, components: components);
    }

    [ComponentInteraction("pets:rename:*:*", true)]
    public async Task PetRenameButtonAsync(string invokerId, string itemKey)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🐾 Use `/pets` para abrir seus próprios pets.", ephemeral: true);
            return;
        }

        var catalog = await _shop.GetCatalogAsync();
        var pet = catalog.FirstOrDefault(c => c.Key == itemKey && c.Category == ItemCategory.Pet);
        if (pet == null)
        {
            await RespondAsync("❌ Pet não encontrado.", ephemeral: true);
            return;
        }

        await RespondWithModalAsync<PetRenameModal>($"{PetsPrefix}:rename:{Context.User.Id}:{itemKey}");
    }

    [ModalInteraction("pets:rename:*:*")]
    public async Task PetRenameModalAsync(string invokerId, string itemKey, PetRenameModal modal)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("🐾 Use `/pets` para abrir seus próprios pets.", ephemeral: true);
            return;
        }

        var (success, message) = await _shop.RenamePetAsync(Context.User.Id, itemKey, CleanNickname(modal.Apelido));
        await RespondAsync(success ? $"✅ {message}" : message!, ephemeral: true);
    }

    private async Task<(Embed Embed, MessageComponent Components)> BuildPetsScreenAsync()
    {
        var catalog = await _shop.GetCatalogAsync();
        var inventory = await _shop.GetInventoryAsync(Context.User.Id);

        var ownedPets = inventory
            .Where(inv => catalog.Any(c => c.Key == inv.ItemKey && c.Category == ItemCategory.Pet))
            .OrderBy(inv => inv.ItemKey)
            .ToList();

        var sb = new StringBuilder();

        if (ownedPets.Count == 0)
        {
            sb.AppendLine("🔎 Você ainda não tem pets. Adote um com `/loja` e evolua-o com os **Consumíveis**!");
        }
        else
        {
            sb.AppendLine($"🎒 Slots de pet: **{ownedPets.Count}/{MaxPetSlots}**\n");

            foreach (var inv in ownedPets)
            {
                var pet = catalog.First(c => c.Key == inv.ItemKey);

                sb.AppendLine(PetFormatter.PetName(pet, inv));
                sb.AppendLine($"  └ {PetFormatter.ProgressLine(pet, inv.Quantity)}");

                var evolution = PetFormatter.EvolutionLine(pet, inv.Quantity);
                if (!string.IsNullOrEmpty(evolution))
                    sb.AppendLine($"  └ {evolution}");

                sb.AppendLine();
            }

            sb.AppendLine("🧪 Suba o nível de cada pet comprando o consumível dele em `/loja`.");
        }

        var components = new ComponentBuilder();
        for (var i = 0; i < ownedPets.Count; i++)
        {
            var pet = catalog.First(c => c.Key == ownedPets[i].ItemKey);
            components.WithButton(new ButtonBuilder()
                .WithLabel($"✏️ Renomear {pet.Name}")
                .WithCustomId($"{PetsPrefix}:rename:{Context.User.Id}:{ownedPets[i].ItemKey}")
                .WithStyle(ButtonStyle.Secondary), i / 5);
        }

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Pets", Context.User.GetAvatarUrl())
            .WithTitle("🐾 Seus Pets")
            .WithDescription(sb.ToString())
            .Build();

        return (embed, components.Build());
    }

    private static string? CleanNickname(string raw)
    {
        var value = raw.Trim().Replace("\r", " ").Replace("\n", " ");
        if (value.Length == 0) return null;
        return value.Length > 32 ? value[..32] : value;
    }
}

public class PetRenameModal : IModal
{
    public string Title => "Renomear pet";

    [InputLabel("Apelido do pet (vazio limpa o apelido)")]
    [ModalTextInput("pet_nome", TextInputStyle.Short, maxLength: 32)]
    public string Apelido { get; set; } = string.Empty;
}