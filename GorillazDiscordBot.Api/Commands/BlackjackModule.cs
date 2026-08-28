using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class BlackjackModule : ModuleBase<SocketCommandContext>
{
    private readonly IEconomyRepository _economy;
    private readonly GameSessionManager _sessions;

    public BlackjackModule(IEconomyRepository economy, GameSessionManager sessions)
    {
        _economy = economy;
        _sessions = sessions;
    }

    [Command("blackjack")]
    [Alias("bj")]
    [Summary("Inicia uma mão de Blackjack. Uso: macaco blackjack <valor>")]
    public async Task BlackjackAsync(string valor)
    {
        if (!EconomyModule.EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        await TryResolveExpiredAsync();

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await ReplyAsync("🃏 Você já tem uma mão em andamento! Use `macaco hit`, `macaco stand` ou `macaco double`.");
            return;
        }

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            Context.User.Id, quantia, EconomyTransactionType.Bet, "Aposta no blackjack");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        var game = new BlackjackGame(quantia);

        if (game.Phase == BlackjackPhase.Finished)
        {
            await SettleAsync(game);
            return;
        }

        _sessions.Add(Context.User.Id, game);

        await ReplyAsync(embed: BuildTableEmbed(game));
    }

    [Command("hit")]
    [Alias("pedir")]
    [Summary("Pede mais uma carta na mão de Blackjack")]
    public async Task HitAsync()
    {
        var game = await GetPlayableSessionAsync();
        if (game == null) return;

        game.Hit();
        _sessions.Touch(Context.User.Id);

        if (game.Phase == BlackjackPhase.Finished)
            await SettleAsync(game);
        else
            await ReplyAsync(embed: BuildTableEmbed(game));
    }

    [Command("stand")]
    [Alias("parar")]
    [Summary("Para de pedir cartas e encerra a mão de Blackjack")]
    public async Task StandAsync()
    {
        var game = await GetPlayableSessionAsync();
        if (game == null) return;

        game.Stand();
        await SettleAsync(game);
    }

    [Command("double")]
    [Alias("dobrar")]
    [Summary("Dobra a aposta, recebe uma carta e para (só nas 2 primeiras cartas)")]
    public async Task DoubleDownAsync()
    {
        var game = await GetPlayableSessionAsync();
        if (game == null) return;

        if (game.Player.Cards.Count != 2 || game.Doubled)
        {
            await ReplyAsync("⚠️ Dobrar é permitido apenas com as duas primeiras cartas.");
            return;
        }

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            Context.User.Id, game.Bet, EconomyTransactionType.Bet, "Double no blackjack");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira para dobrar.");
            return;
        }

        game.DoubleDown();
        await SettleAsync(game);
    }

    private async Task<BlackjackGame?> GetPlayableSessionAsync()
    {
        if (_sessions.GetActive(Context.User.Id) is { } active)
            return active;

        await TryResolveExpiredAsync();
        return null;
    }

    private async Task<bool> TryResolveExpiredAsync()
    {
        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired == null) return false;

        expired.Stand();
        await SettleAsync(expired, prefix: "⌛ Sua mão anterior expirou e o dealer jogou por você.\n\n");
        return true;
    }

    private async Task SettleAsync(BlackjackGame game, string prefix = "")
    {
        _sessions.Remove(Context.User.Id);

        var totalReturn = game.CalculateTotalReturn();

        if (totalReturn > 0)
            await _economy.AddMoneyAsync(Context.User.Id, totalReturn, EconomyTransactionType.Bet, "Pagamento do blackjack");

        var user = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        var resultSection = prefix
            + BlackjackTableBuilder.DescribeResult(game, totalReturn)
            + $"\n💰 Saldo atual: **{user.Money}** moedas";

        await ReplyAsync(embed: BuildTableEmbed(game, resultSection));
    }

    private Embed BuildTableEmbed(BlackjackGame game, string? resultSection = null)
        => BlackjackTableBuilder.BuildTable(game, Context.User, resultSection);
}
