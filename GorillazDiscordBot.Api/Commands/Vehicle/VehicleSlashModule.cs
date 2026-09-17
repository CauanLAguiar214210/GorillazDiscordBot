using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Vehicle;

[Group("veiculo", "Sua habilitação e seus veículos no jogo")]
public partial class VehicleSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ShopService _shop;

    public VehicleSlashModule(ShopService shop)
    {
        _shop = shop;
    }

    [SlashCommand("dirigir", "Equipa um veículo que você possui (exige a licença)")]
    public async Task DirigirAsync(
        [Summary("veiculo", "ID ou nome do veículo")] string veiculo)
    {
        var (success, message) = await _shop.DriveVehicleAsync(
            Context.User.Id, Context.User.Username, veiculo);

        await RespondAsync(success ? $"✅ {message}" : message!, ephemeral: !success);
    }

    [SlashCommand("estacionar", "Guarda o veículo atual na garagem")]
    public async Task EstacionarAsync()
    {
        var (success, message) = await _shop.ParkVehicleAsync(Context.User.Id);

        await RespondAsync(success ? $"✅ {message}" : message!, ephemeral: !success);
    }

    [SlashCommand("atual", "Mostra o veículo que você está dirigindo")]
    public async Task AtualAsync()
    {
        var (vehicle, _) = await _shop.GetCurrentVehicleAsync(Context.User.Id);

        var description = vehicle != null
            ? $"Você está dirigindo **{vehicle.Emoji} {vehicle.Name}**!"
            : "Você está a pé. Compre um veículo na `/loja` e use `/veiculo dirigir`.";

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Veículo atual", Context.User.GetAvatarUrl())
            .WithTitle("🚙 Veículo atual")
            .WithDescription(description)
            .WithStandardFooter("Use /veiculo estacionar para guardar o veículo")
            .Build();

        await RespondAsync(embed: embed);
    }
}