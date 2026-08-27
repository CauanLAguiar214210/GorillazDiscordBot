using System;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Slash;

public class CasinoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IUserRepository _userRepository;
    private readonly CasinoService _casino;
    private readonly GameSessionManager _sessions;

    public CasinoSlashModule(IUserRepository userRepository, CasinoService casino, GameSessionManager sessions)
    {
        _userRepository = userRepository;
        _casino = casino;
        _sessions = sessions;
    }

    [SlashCommand("bet", "Aposte moedas em cara ou coroa (50/50)")]
    public async Task BetAsync([Summary("valor", "Valor da aposta ou 'all' para apostar tudo")] string valor)
    {
        var (amount, error) = await ResolveAmountAsync(Context.User.Id, valor);
        if (error != null)
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var result = await _casino.PlaceSimpleBetAsync(Context.User.Id, amount);

        if (!result.Success)
        {
            await RespondAsync(result.Error!, ephemeral: true);
            return;
        }

        await RespondAsync(
            BetResultText(result.Won, amount, result.Balance),
            components: CasinoTableBuilder.BuildReplayComponents("bet", amount.ToString()));
    }

    [SlashCommand("blackjack", "Inicia uma mão de Blackjack com botões")]
    public async Task BlackjackAsync([Summary("valor", "Valor da aposta ou 'all' para apostar tudo")] string valor)
    {
        var (amount, error) = await ResolveAmountAsync(Context.User.Id, valor);
        if (error != null)
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var userId = Context.User.Id;

        var expired = _sessions.TakeExpired(userId);
        if (expired != null)
            expired.Stand();

        if (_sessions.GetActive(userId) != null)
        {
            await RespondAsync("🃏 Você já tem uma mão em andamento! Use os botões da mesa aberta.", ephemeral: true);
            return;
        }

        var game = new BlackjackGame(amount);

        if (game.Phase == BlackjackPhase.Finished)
        {
            var settledEmbed = await SettleAndBuildAsync(game);
            await RespondAsync(embed: settledEmbed, components: BjReplayComponents(amount));

            if (expired != null)
                await ReportExpiredAsync(expired);

            return;
        }

        _sessions.Add(userId, game);

        await RespondAsync(
            embed: BlackjackTableBuilder.BuildTable(game, Context.User),
            components: BlackjackTableBuilder.BuildActionComponents(game));

        if (expired != null)
            await ReportExpiredAsync(expired);
    }

    [ComponentInteraction("bj:*", true)]
    public async Task BlackjackActionAsync(string action)
    {
        await DeferAsync();

        var userId = Context.User.Id;

        var expired = _sessions.TakeExpired(userId);
        if (expired != null)
        {
            expired.Stand();
            var expiredEmbed = await SettleAndBuildAsync(expired, "⌛ Sua mão anterior expirou e o dealer jogou por você.\n\n");
            await ReplaceWithResultAsync(expiredEmbed);
            return;
        }

        var game = _sessions.GetActive(userId);

        if (game == null)
        {
            await FollowupAsync("🃏 Esta mesa não tem mais uma mão ativa ou não é sua. Use `/blackjack` para começar outra.", ephemeral: true);
            return;
        }

        switch (action)
        {
            case BlackjackTableBuilder.HitAction:
                game.Hit();
                _sessions.Touch(userId);
                break;

            case BlackjackTableBuilder.StandAction:
                game.Stand();
                break;

            case BlackjackTableBuilder.DoubleAction:
                if (!BlackjackTableBuilder.CanDouble(game))
                {
                    await FollowupAsync("⚠️ Dobrar é permitido apenas com as duas primeiras cartas.", ephemeral: true);
                    return;
                }

                var (deducted, _, error) = await _casino.ReserveAsync(userId, game.Bet);
                if (!deducted)
                {
                    await FollowupAsync(error!, ephemeral: true);
                    return;
                }

                game.DoubleDown();
                break;

            default:
                await FollowupAsync("Ação desconhecida.", ephemeral: true);
                return;
        }

        if (game.Phase == BlackjackPhase.Finished)
        {
            var embed = await SettleAndBuildAsync(game);
            await ReplaceWithResultAsync(embed, BjReplayComponents(game.Bet));
        }
        else
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BlackjackTableBuilder.BuildTable(game, Context.User);
                m.Components = BlackjackTableBuilder.BuildActionComponents(game);
            });
        }
    }

    [SlashCommand("roleta", "Roleta da Selva")]
    public async Task RoletaAsync(
        [Summary("tipo", "Tipo de aposta")]
        [Choice("Número (0-36)", "numero")]
        [Choice("Vermelho", "vermelho")]
        [Choice("Preto", "preto")]
        [Choice("Par", "par")]
        [Choice("Ímpar", "impar")]
        [Choice("Baixo (1-18)", "baixo")]
        [Choice("Alto (19-36)", "alto")]
        string tipo,
        [Summary("valor", "Valor da aposta ou 'all' para apostar tudo")] string valor,
        [Summary("numero", "Número de 0 a 36 (somente para aposta Número)")] int numero = -1)
    {
        var (amount, verror) = await ResolveAmountAsync(Context.User.Id, valor);
        if (verror != null)
        {
            await RespondAsync(verror, ephemeral: true);
            return;
        }

        if (tipo == "numero" && (numero < 0 || numero > 36))
        {
            await RespondAsync("⚠️ Para aposta em número, informe um valor de 0 a 36 no parâmetro `numero`.", ephemeral: true);
            return;
        }

        var bet = BuildRoletaBet(tipo, numero);

        var (deducted, _, rerror) = await _casino.ReserveAsync(Context.User.Id, amount);
        if (!deducted)
        {
            await RespondAsync(rerror!, ephemeral: true);
            return;
        }

        var game = new RouletteGame();
        var winAmount = game.Evaluate(bet) * amount;

        if (winAmount > 0)
            await _casino.CreditAsync(Context.User.Id, winAmount);

        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await RespondAsync(
            embed: RouletteTableBuilder.BuildResult(game, bet, winAmount, user.Money),
            components: CasinoTableBuilder.BuildReplayComponents("roleta", $"{tipo}:{amount}:{numero}"));
    }

    [SlashCommand("slots", "Caça-níqueis da Selva")]
    public async Task SlotsAsync([Summary("valor", "Valor da aposta ou 'all' para apostar tudo")] string valor)
    {
        var (amount, error) = await ResolveAmountAsync(Context.User.Id, valor);
        if (error != null)
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var (deducted, _, derror) = await _casino.ReserveAsync(Context.User.Id, amount);
        if (!deducted)
        {
            await RespondAsync(derror!, ephemeral: true);
            return;
        }

        var game = new SlotGame();
        var winAmount = game.Multiplier * amount;

        if (winAmount > 0)
            await _casino.CreditAsync(Context.User.Id, winAmount);

        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await RespondAsync(
            embed: SlotMachineBuilder.BuildResult(game, winAmount, user.Money),
            components: CasinoTableBuilder.BuildReplayComponents("slots", amount.ToString()));
    }

    [ComponentInteraction("casino:again:*", true)]
    public async Task CasinoAgainModalAsync(string data)
    {
        var (_, gameKey, parts) = CasinoTableBuilder.Decode($"again:{data}");
        var customId = $"casino:submit:{gameKey}:{string.Join(":", parts)}";

        var modal = new ModalBuilder()
            .WithCustomId(customId)
            .WithTitle("🎰 Nova aposta")
            .AddTextInput(new TextInputBuilder(
                "Valor da aposta", "valor", TextInputStyle.Short, "100 ou all", 1, 20, true, null))
            .Build();

        await RespondWithModalAsync(modal);
    }

    [ComponentInteraction("casino:allin:*", true)]
    public async Task CasinoAllInAsync(string data)
    {
        await DeferAsync();

        var (_, gameKey, parts) = CasinoTableBuilder.Decode($"allin:{data}");
        var wallet = (await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username)).Money;

        if (wallet <= 0)
        {
            await FailAsync("❌ Você não tem moedas na carteira para apostar tudo.");
            return;
        }

        await PlayGameAsync(gameKey, parts, wallet);
    }

    [ModalInteraction("casino:submit:*", true)]
    public async Task CasinoSubmitAsync(string data)
    {
        var modal = (SocketModal)Context.Interaction;
        var valor = modal.Data.Components.First(c => c.CustomId == "valor").Value;

        var (_, gameKey, parts) = CasinoTableBuilder.Decode($"submit:{data}");
        var (amount, error) = await ResolveAmountAsync(Context.User.Id, valor.Trim());

        if (error != null)
        {
            await FailAsync(error);
            return;
        }

        await PlayGameAsync(gameKey, parts, amount);
    }

    private async Task PlayGameAsync(string gameKey, string[] parts, int amount)
    {
        var (deducted, _, error) = await _casino.ReserveAsync(Context.User.Id, amount);
        if (!deducted)
        {
            await FailAsync(error!);
            return;
        }

        switch (gameKey)
        {
            case "bet":
                await ReplayBetAsync(amount);
                break;
            case "slots":
                await ReplaySlotsAsync(amount);
                break;
            case "roleta":
                await ReplayRoletaAsync(parts, amount);
                break;
            case "bj":
                await ReplayBlackjackAsync(amount);
                break;
        }
    }

    private async Task FailAsync(string message)
    {
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Content = message;
            m.Embed = null;
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task ReplayBetAsync(int amount)
    {
        var result = await _casino.PlaceSimpleBetAsync(Context.User.Id, amount);

        if (!result.Success)
        {
            await FailAsync(result.Error!);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Content = BetResultText(result.Won, amount, result.Balance);
            m.Embed = null;
            m.Components = CasinoTableBuilder.BuildReplayComponents("bet", amount.ToString());
        });
    }

    private async Task ReplaySlotsAsync(int amount)
    {
        var game = new SlotGame();
        var winAmount = game.Multiplier * amount;

        if (winAmount > 0)
            await _casino.CreditAsync(Context.User.Id, winAmount);

        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        var embed = SlotMachineBuilder.BuildResult(game, winAmount, user.Money);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = CasinoTableBuilder.BuildReplayComponents("slots", amount.ToString());
        });
    }

    private async Task ReplayRoletaAsync(string[] parts, int amount)
    {
        var tipo = parts[0];
        int numero = parts.Length > 2 ? int.Parse(parts[2]) : -1;
        var bet = BuildRoletaBet(tipo, numero);

        var game = new RouletteGame();
        var winAmount = game.Evaluate(bet) * amount;

        if (winAmount > 0)
            await _casino.CreditAsync(Context.User.Id, winAmount);

        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        var embed = RouletteTableBuilder.BuildResult(game, bet, winAmount, user.Money);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = CasinoTableBuilder.BuildReplayComponents("roleta", $"{tipo}:{amount}:{numero}");
        });
    }

    private async Task ReplayBlackjackAsync(int amount)
    {
        var game = new BlackjackGame(amount);

        if (game.Phase == BlackjackPhase.Finished)
        {
            var embed = await SettleAndBuildAsync(game);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = embed;
                m.Components = BjReplayComponents(amount);
            });
            return;
        }

        _sessions.Add(Context.User.Id, game);
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BlackjackTableBuilder.BuildTable(game, Context.User);
            m.Components = BlackjackTableBuilder.BuildActionComponents(game);
        });
    }

    private async Task<(int amount, string? error)> ResolveAmountAsync(ulong userId, string raw)
    {
        if (raw is "all" or "tudo" or "allin")
        {
            var user = await _userRepository.GetOrCreateAsync(userId, Context.User.Username);
            if (user.Money <= 0)
                return (0, "❌ Você não tem moedas na carteira para apostar tudo.");

            return (user.Money, null);
        }

        if (!int.TryParse(raw, out int amount) || amount <= 0)
            return (0, "⚠️ Informe um valor numérico positivo ou `all` para apostar tudo.");

        return (amount, null);
    }

    private static RouletteBet BuildRoletaBet(string tipo, int numero) =>
        tipo == "numero" ? RouletteBet.OnNumber(numero)
        : tipo switch
        {
            "vermelho" => RouletteBet.OnColor(RouletteColor.Red),
            "preto" => RouletteBet.OnColor(RouletteColor.Black),
            "par" => RouletteBet.OnEvenOdd(true),
            "impar" => RouletteBet.OnEvenOdd(false),
            "baixo" => RouletteBet.OnHalf(true),
            _ => RouletteBet.OnHalf(false)
        };

    private static string BetResultText(bool won, int amount, int balance) =>
        won
            ? $"🎉 **Você ganhou!** +{amount} moedas! Saldo: **{balance}**"
            : $"😢 **Você perdeu!** -{amount} moedas! Saldo: **{balance}**";

    private static MessageComponent BjReplayComponents(int amount) =>
        CasinoTableBuilder.BuildReplayComponents("bj", amount.ToString());

    private async Task<Embed> SettleAndBuildAsync(BlackjackGame game, string prefix = "")
    {
        _sessions.Remove(Context.User.Id);

        var totalReturn = game.CalculateTotalReturn();

        if (totalReturn > 0)
            await _casino.CreditAsync(Context.User.Id, totalReturn);

        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        var resultSection = prefix
            + BlackjackTableBuilder.DescribeResult(game, totalReturn)
            + $"\n💰 Saldo atual: **{user.Money}** moedas";

        return BlackjackTableBuilder.BuildTable(game, Context.User, resultSection);
    }

    private Task ReplaceWithResultAsync(Embed embed, MessageComponent? components = null)
        => Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components ?? new ComponentBuilder().Build();
        });

    private async Task ReportExpiredAsync(BlackjackGame expired)
    {
        var embed = await SettleAndBuildAsync(expired, "⌛ Sua mão anterior expirou e o dealer jogou por você.\n\n");
        await FollowupAsync(embed: embed, ephemeral: true);
    }

    public static class CasinoTableBuilder
    {
        public static MessageComponent BuildReplayComponents(string gameKey, string encodedParams)
        {
            var againId = $"casino:again:{gameKey}:{encodedParams}";
            var allInId = $"casino:allin:{gameKey}:{encodedParams}";

            return new ComponentBuilder()
                .WithButton("🎰 Tentar de novo", againId, ButtonStyle.Primary)
                .WithButton("💰 All-in", allInId, ButtonStyle.Danger)
                .Build();
        }

        public static (string mode, string gameKey, string[] parts) Decode(string data)
        {
            var segments = data.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                return (string.Empty, string.Empty, Array.Empty<string>());

            return (segments[0], segments[1], segments.Skip(2).ToArray());
        }
    }
}
