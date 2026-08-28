using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public class CasinoModule : ModuleBase<SocketCommandContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public CasinoModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [Command("casino")]
    [Alias("cassino")]
    [Summary("Mostra os jogos disponíveis no cassino")]
    public async Task CasinoOverviewAsync()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("🎰 **Cassino do Goriláz**\n");
        sb.AppendLine("`roleta <valor> [tipo] [alvo]` — Aposta em número, cor, par/ímpar ou metade");
        sb.AppendLine("`cacaniquel <valor>` (alias: slot) — Caça-níquel com multiplicadores");
        sb.AppendLine();
        sb.AppendLine("Disponível também por `/roleta` e `/cacaniquel`.");

        await ReplyAsync(embed: new EmbedBuilder()
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithTitle("\U0001F3B0 Cassino")
            .Build());
    }

    [Command("roleta")]
    [Alias("roulette")]
    [Summary("Aposta na roleta. Uso: macaco roleta <valor> [tipo] [alvo]")]
    public async Task RouletteAsync(string valor, string? tipo = null, string? alvo = null)
    {
        if (!EconomyModule.EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        await TryResolveExpiredAsync();

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await ReplyAsync("🎰 Você já tem uma roleta em andamento! Use os botões da mesa aberta.");
            return;
        }

        var betType = RouletteBetType.Number;
        var target = 0;

        if (!TryParseBetType(tipo, alvo, out betType, out target, out var typeError))
        {
            await ReplyAsync(typeError!);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, quantia, Context.User.Username, "Aposta na roleta");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        var game = new RouletteGame();
        game.AddBet(quantia, betType, target);

        var session = CasinoSession.ForRoulette(quantia, game);
        _sessions.Add(Context.User.Id, session);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await ReplyAsync(
            embed: CasinoTableBuilder.BuildRouletteTable(game, Context.User, balance),
            components: CasinoTableBuilder.BuildRouletteComponents(game.Bets.Count > 0));
    }

    [Command("cacaniquel")]
    [Alias("slot", "slots")]
    [Summary("Joga na caça-níquel. Uso: macaco cacaniquel <valor>")]
    public async Task SlotAsync(string valor)
    {
        if (!EconomyModule.EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        await TryResolveExpiredAsync();

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await ReplyAsync("🎰 Você já tem uma máquina em andamento! Use o botão da máquina aberta.");
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, quantia, Context.User.Username, "Aposta na caça-níquel");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        var slots = new SlotMachineGame();
        var session = CasinoSession.ForSlots(quantia, slots);
        _sessions.Add(Context.User.Id, session);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await ReplyAsync(
            embed: CasinoTableBuilder.BuildSlotTable(slots, quantia, Context.User, balance),
            components: CasinoTableBuilder.BuildSlotComponents(slots.HasSpun));
    }

    private async Task TryResolveExpiredAsync()
    {
        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired == null) return;

        if (expired.Roulette != null && expired.Roulette.Bets.Count > 0)
        {
            expired.Roulette.Spin();
            var returnAmount = expired.Roulette.CalculateTotalReturn();
            var balance = await _play.PayOutAsync(
                Context.User.Id, returnAmount, Context.User.Username, "Roleta expirada");
        }
        else if (expired.Slots != null && !expired.Slots.HasSpun)
        {
            expired.Slots.Spin();
            var returnAmount = SlotMachineGame.CalculateReturn(expired.Bet, expired.Slots.Reels);
            await _play.PayOutAsync(
                Context.User.Id, returnAmount, Context.User.Username, "Caça-níquel expirada");
        }
    }

    private static bool TryParseBetType(
        string? tipo, string? alvo,
        out RouletteBetType betType, out int target, out string? error)
    {
        betType = RouletteBetType.Number;
        target = 0;
        error = null;

        if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(alvo))
        {
            error = "⚠️ Use `roleta <valor> <tipo> <alvo>`. Tipos: `numero`, `vermelho`, `preto`, `par`, `impar`, `baixa`, `alta`.";
            return false;
        }

        switch (tipo.ToLowerInvariant())
        {
            case "numero":
            case "number":
                if (!int.TryParse(alvo, out target) || target < 0 || target > 36)
                {
                    error = "⚠️ O alvo do número deve estar entre 0 e 36.";
                    return false;
                }
                betType = RouletteBetType.Number;
                return true;

            case "vermelho":
            case "red":
                betType = RouletteBetType.Color;
                target = (int)RouletteColor.Red;
                return true;

            case "preto":
            case "black":
                betType = RouletteBetType.Color;
                target = (int)RouletteColor.Black;
                return true;

            case "par":
            case "even":
                betType = RouletteBetType.Parity;
                target = 0;
                return true;

            case "impar":
            case "odd":
                betType = RouletteBetType.Parity;
                target = 1;
                return true;

            case "baixa":
            case "low":
                betType = RouletteBetType.Half;
                target = 0;
                return true;

            case "alta":
            case "high":
                betType = RouletteBetType.Half;
                target = 1;
                return true;

            default:
                error = "⚠️ Tipo inválido. Use `numero`, `vermelho`, `preto`, `par`, `impar`, `baixa`, `alta`.";
                return false;
        }
    }
}
