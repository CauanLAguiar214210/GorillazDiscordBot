using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Economy;

public partial class BancoSlashModule
{
    [Group("ativos", "Ativos de renda comprados por cotas")]
    public class ComandosAtivos : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IEconomyRepository _economy;
        private readonly IEconomyAccessor _accessor;
        private readonly ShopService _shop;

        public ComandosAtivos(IEconomyRepository economy, IEconomyAccessor accessor, ShopService shop)
        {
            _economy = economy;
            _accessor = accessor;
            _shop = shop;
        }

        [SlashCommand("listar", "Mostra o mercado de ativos e suas posições")]
        public async Task ListarAsync()
        {
            var catalog = await _shop.GetCatalogAsync();
            var market = catalog
                .Where(c => c.Category == ItemCategory.Asset && c.IsActive && !c.IsPlaceholder)
                .OrderBy(c => c.SortOrder)
                .ToList();

            var positions = await _shop.GetOwnedAssetPositionsAsync(Context.User.Id);
            var ownedByKey = positions.ToDictionary(p => p.Asset.Key, StringComparer.OrdinalIgnoreCase);

            var sb = new StringBuilder();
            foreach (var item in market)
            {
                var price = EconomyRules.ComputeCotaPrice(item.Key, item.Price);
                var variation = EconomyRules.GetPriceVariation(item.Key, DateTime.UtcNow.Date);
                var income = EconomyRules.ComputeQuotaIncome(item.DailyIncome, EconomyRules.AssetQuotasPerShare);

                var vMark = variation >= 0 ? "📈" : "📉";
                sb.AppendLine(
                    $"{item.Emoji} **{item.Name}** — cota **{EconomyFormat.Full(price)}** · " +
                    $"{vMark} {variation:+0.0;-0.0}% hoje · +{EconomyFormat.Full(income)}/dia (100 cotas)");

                if (ownedByKey.TryGetValue(item.Key, out var position))
                {
                    var value = EconomyRules.ComputeQuotaPrice(item.Key, item.Price, position.Owned.Quantity);
                    var ownIncome = EconomyRules.ComputeQuotaIncome(item.DailyIncome, position.Owned.Quantity);
                    sb.AppendLine(
                        $"   ✔️ Você: **{position.Owned.Quantity} cotas** · valor **{EconomyFormat.Full(value)}** · " +
                        $"+{EconomyFormat.Full(ownIncome)}/dia");
                }
            }

            await RespondAsync(embed: new EmbedBuilder()
                .WithGoldTheme()
                .WithAuthor($"📈 Mercado de Ativos — {Context.User.GetDisplayName()}", Context.User.GetAvatarUrl())
                .WithDescription(sb.ToString())
                .WithStandardFooter("Cota por ativo é fixa (100) e o preço varia só a cada dia")
                .Build());
        }

        [SlashCommand("comprar", "Compra cotas de um ativo pelo preço do dia")]
        public async Task ComprarAsync(
            [Summary("ativo", "Nome do ativo")] string ativo,
            [Summary("cotas", "Quantas cotas comprar (1-100)")]
            [MinValue(1)] [MaxValue(100)] int cotas = 1)
        {
            var item = await FindAssetAsync(ativo);
            if (item is null)
            {
                await RespondAsync($"❌ Você não possui **{ativo}**. Use `/banco ativos listar`.", ephemeral: true);
                return;
            }

            var (success, message, balance) = await _shop.BuyAssetAsync(
                Context.User.Id, Context.User.Username, item, cotas);

            if (!success)
            {
                await RespondAsync(message!, ephemeral: true);
                return;
            }

            var price = EconomyRules.ComputeQuotaPrice(item.Key, item.Price, cotas);
            await RespondAsync(
                $"✅ **{Context.User.GetDisplayName()}** comprou **{cotas} cota(s)** de **{item.Emoji} {item.Name}** " +
                $"por **{EconomyFormat.Full(price)} moedas**!\nCarteira: **{EconomyFormat.Full(balance)} moedas**");
        }

        [SlashCommand("vender", "Vende cotas de um ativo pelo preço do dia")]
        public async Task VenderAsync(
            [Summary("ativo", "Nome do ativo")] string ativo,
            [Summary("cotas", "Quantas cotas vender (1-100)")]
            [MinValue(1)] [MaxValue(100)] int cotas = 1)
        {
            var item = await FindAssetAsync(ativo);
            if (item is null)
            {
                await RespondAsync($"❌ Não encontrei o ativo **{ativo}**. Use `/banco ativos listar`.", ephemeral: true);
                return;
            }

            var owned = await _shop.GetOwnedAssetPositionsAsync(Context.User.Id);
            var position = owned.FirstOrDefault(p =>
                p.Asset.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase));

            if (position.Owned is null || position.Owned.Quantity <= 0)
            {
                await RespondAsync($"❌ Você não possui **{item.Emoji} {item.Name}**. Compre em `/banco ativos comprar`.", ephemeral: true);
                return;
            }

            if (cotas > position.Owned.Quantity)
            {
                await RespondAsync(
                    $"❌ Você possui apenas **{position.Owned.Quantity} cotas** de **{item.Emoji} {item.Name}**.", ephemeral: true);
                return;
            }

            var (success, message, balance) = await _shop.SellAssetAsync(
                Context.User.Id, Context.User.Username, item, cotas);

            if (!success)
            {
                await RespondAsync(message!, ephemeral: true);
                return;
            }

            var price = EconomyRules.ComputeQuotaPrice(item.Key, item.Price, cotas);
            await RespondAsync(
                $"💹 **{Context.User.GetDisplayName()}** vendeu **{cotas} cota(s)** de **{item.Emoji} {item.Name}** " +
                $"por **{EconomyFormat.Full(price)} moedas**!\nCarteira: **{EconomyFormat.Full(balance)} moedas**");
        }

        private async Task<ShopItem?> FindAssetAsync(string input)
        {
            var catalog = await _shop.GetCatalogAsync();
            return catalog.FirstOrDefault(c =>
                c.Category == ItemCategory.Asset
                && (c.Key.Equals(input, StringComparison.OrdinalIgnoreCase)
                    || c.Name.Equals(input, StringComparison.OrdinalIgnoreCase)));
        }
    }
}