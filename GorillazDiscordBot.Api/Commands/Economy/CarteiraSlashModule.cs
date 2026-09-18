using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Economy;

[Group("carteira", "Sua carteira: saldo, patrimônio e transferências")]
public class CarteiraSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;
    private readonly IPatrimonioService _patrimonio;

    public CarteiraSlashModule(
        IEconomyRepository economy,
        IEconomyAccessor accessor,
        IPatrimonioService patrimonio)
    {
        _economy = economy;
        _accessor = accessor;
        _patrimonio = patrimonio;
    }

    [SlashCommand("ver", "Mostra o saldo da sua carteira e seu patrimônio")]
    public async Task VerAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);
        var snapshot = await _patrimonio.GetSnapshotAsync(mainId, Context.User.Username);

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Carteira", Context.User.GetAvatarUrl())
            .WithDescription(
                $"💰 **Carteira:** {EconomyFormat.Full(profile.Money)} moedas\n" +
                $"🏦 **Banco:** {EconomyFormat.Full(profile.Bank)} moedas\n" +
                $"💰 **Poupança:** {EconomyFormat.Full(profile.Savings)} moedas\n" +
                $"💎 **Patrimônio:** {EconomyFormat.Compact(snapshot.Total)} moedas\n\n" +
                "Guarde moedas no banco ou na poupança para render juros e se proteger do `roubar`. Veja tudo em `/banco ver`.")
            .WithStandardFooter("Use /banco para depositar, poupar e consultar ativos")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("pagar", "Transfere moedas da sua carteira para outro usuário")]
    public async Task PagarAsync(Discord.IUser receiver, [Summary("valor", "Valor ou 'tudo'")] string valor)
    {
        if (receiver.IsBot)
        {
            await RespondAsync("🤖 Não posso aceitar moedas, mas obrigado!", ephemeral: true);
            return;
        }

        if (receiver.Id == Context.User.Id)
        {
            await RespondAsync("😂 Não dá pra pagar você mesmo.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (!TryResolveAmount(valor, profile.Money, out var quantia, out var error))
        {
            await RespondAsync(error!, ephemeral: true);
            return;
        }

        var receiverMain = await _accessor.ResolveMainIdAsync(receiver.Id);

        if (mainId == receiverMain)
        {
            await RespondAsync("🔗 A conta de destino faz parte do seu próprio grupo vinculado.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            mainId, quantia, EconomyTransactionType.Payment,
            $"Pagamento para {receiver.GetDisplayName()}");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        await _economy.GetOrCreateAsync(receiverMain, receiver.Username);
        await _economy.AddMoneyAsync(receiverMain, quantia, EconomyTransactionType.Payment,
            $"Pagamento de {Context.User.GetDisplayName()}");

        await RespondAsync($"💸 **{Context.User.GetDisplayName()}** pagou **{EconomyFormat.Full(quantia)} moedas** para **{receiver.GetDisplayName()}**!");
    }

    private static bool TryResolveAmount(string input, ulong? balance, out ulong amount, out string? error)
    {
        if (input.Equals("tudo", StringComparison.OrdinalIgnoreCase)
            || input.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (balance is null or 0)
            {
                amount = 0;
                error = "⚠️ Você não tem moedas para isso.";
                return false;
            }

            amount = balance.Value;
            error = null;
            return true;
        }

        return EconomyAmountParser.TryParse(input, out amount, out error);
    }
}