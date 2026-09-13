using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using LuckyMonkey.Contracts.Bets;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;

namespace GorillazDiscordBot.Commands.Casino;

/// <summary>
/// Atalhos para falar com o microserviço de cassino já tratando os erros de protocolo
/// (sessão ativa, ausente ou expirada) com uma mensagem amigável por jogo.
/// Ao receber <see cref="ErrorCode.ActiveSession"/>, encerra a rodada pendente do usuário,
/// paga o resultado e reabre a nova aposta automaticamente.
/// </summary>
internal static class CasinoApiFlow
{
    public static async Task<BetResponse?> OpenBetAsync(
        InteractionModuleBase<SocketInteractionContext> module,
        CasinoApiClient casino, PayoutService payout, CasinoBetTracker tracker,
        GameKind game, ulong userId, ulong amount,
        BetOptions? options, string blockedMessage, bool followup = false)
    {
        try
        {
            return await StartWithTrackingAsync(casino, tracker, game, userId, amount, options);
        }
        catch (CasinoApiException ex) when (ex.Code == ErrorCode.ActiveSession)
        {
            if (await TrySettlePendingAsync(module, casino, payout, tracker, userId))
            {
                try
                {
                    return await StartWithTrackingAsync(casino, tracker, game, userId, amount, options);
                }
                catch (CasinoApiException retryEx)
                {
                    await ReplyAsync(module, followup, MessageFor(retryEx, blockedMessage));
                    return null;
                }
            }

            await ReplyAsync(module, followup, blockedMessage);
            return null;
        }
        catch (CasinoApiException ex)
        {
            await ReplyAsync(module, followup, MessageFor(ex, blockedMessage));
            return null;
        }
    }

    public static async Task<ActionResponse?> RunActionAsync(
        InteractionModuleBase<SocketInteractionContext> module,
        CasinoApiClient casino, GameKind game, ulong userId, string action,
        ActionPayload? payload, string inactiveMessage, bool followup = true)
    {
        try
        {
            return await casino.ActionAsync(game, userId, action, payload);
        }
        catch (CasinoApiException ex)
        {
            await ReplyAsync(module, followup, MessageFor(ex, inactiveMessage));
            return null;
        }
    }

    /// <summary>Encerra a sessão sem resultado (usado quando o jogador não conseguiu pagar).</summary>
    public static async Task LeaveAsync(CasinoApiClient casino, GameKind game, ulong userId)
    {
        try
        {
            await casino.ActionAsync(game, userId, "leave");
        }
        catch (CasinoApiException)
        {
            // sessão inexistente/indisponível: nada a fazer.
        }
    }

    /// <summary>
    /// Tenta resolver a rodada pendente do usuário no serviço: dispara a ação que liquida o jogo
    /// (a que o bot guarda no tracker), paga o <see cref="Outcome.ReturnAmount"/> e limpa o estado
    /// local. Retorna false quando não há partida pendente conhecida ou ela não pôde ser liquidada.
    /// </summary>
    private static async Task<bool> TrySettlePendingAsync(
        InteractionModuleBase<SocketInteractionContext> module,
        CasinoApiClient casino, PayoutService payout, CasinoBetTracker tracker,
        ulong userId)
    {
        if (tracker.GetGame(userId) is not { } pending)
            return false;

        try
        {
            var settled = await casino.ActionAsync(pending, userId, ResolveAction(pending));
            var bet = tracker.Get(userId);
            tracker.Remove(userId);

            await payout.PayOutAsync(
                userId, settled.Outcome?.ReturnAmount ?? 0, module.Context.User.Username,
                $"Sessão de {FriendlyName(pending)} finalizada automaticamente",
                RelicType(pending), bet);
            return true;
        }
        catch (CasinoApiException)
        {
            return false;
        }
    }

    private static async Task<BetResponse> StartWithTrackingAsync(
        CasinoApiClient casino, CasinoBetTracker tracker,
        GameKind game, ulong userId, ulong amount, BetOptions? options)
    {
        var opened = await casino.StartBetAsync(game, Guid.NewGuid(), userId, amount, options);
        tracker.Set(userId, amount);
        tracker.SetGame(userId, game);
        return opened;
    }

    private static Task ReplyAsync(
        InteractionModuleBase<SocketInteractionContext> module, bool followup, string message)
        => followup
            ? module.Context.Interaction.FollowupAsync(message, ephemeral: true)
            : module.Context.Interaction.RespondAsync(message, ephemeral: true);

    private static string MessageFor(CasinoApiException ex, string blockedMessage)
        => ex.Code is ErrorCode.ActiveSession or ErrorCode.NoSession or ErrorCode.SessionExpired
            ? blockedMessage
            : ex.Message;

    /// <summary>Ação do serviço que liquida a rodada de cada jogo.</summary>
    private static string ResolveAction(GameKind game) => game switch
    {
        GameKind.Roulette => "spin",
        GameKind.Slots => "spin",
        GameKind.Dice => "roll",
        GameKind.Coin => "flip",
        GameKind.Mines => "cashout",
        GameKind.Aviao => "cashout",
        GameKind.VideoPoker => "draw",
        GameKind.Limbo => "roll",
        GameKind.Rps => "play",
        GameKind.Race => "start",
        GameKind.Plinko => "drop",
        GameKind.Wheel => "spin",
        GameKind.HighLow => "cashout",
        _ => "stand"
    };

    private static RelicGameType RelicType(GameKind game) => game switch
    {
        GameKind.Roulette => RelicGameType.Roulette,
        GameKind.Slots => RelicGameType.Slots,
        GameKind.Dice => RelicGameType.Dice,
        GameKind.Coin => RelicGameType.Coin,
        GameKind.Mines => RelicGameType.Mines,
        GameKind.Aviao => RelicGameType.Aviao,
        GameKind.VideoPoker => RelicGameType.VideoPoker,
        GameKind.Limbo => RelicGameType.Limbo,
        GameKind.Rps => RelicGameType.Rps,
        GameKind.Race => RelicGameType.Race,
        GameKind.Plinko => RelicGameType.Plinko,
        GameKind.Wheel => RelicGameType.Wheel,
        GameKind.HighLow => RelicGameType.HighLow,
        GameKind.Baccarat => RelicGameType.Baccarat,
        _ => RelicGameType.Blackjack
    };

    private static string FriendlyName(GameKind game) => game switch
    {
        GameKind.Roulette => "roleta",
        GameKind.Slots => "caça-níquel",
        GameKind.Dice => "dados",
        GameKind.Coin => "cara ou coroa",
        GameKind.Mines => "minas",
        GameKind.Aviao => "aviaozinho",
        GameKind.VideoPoker => "poker de máquina",
        GameKind.Limbo => "limbo",
        GameKind.Rps => "jokenpô",
        GameKind.Race => "corrida",
        GameKind.Plinko => "plinko",
        GameKind.Wheel => "roda da fortuna",
        GameKind.HighLow => "maior/menor",
        GameKind.Baccarat => "baccarat",
        _ => "blackjack"
    };
}