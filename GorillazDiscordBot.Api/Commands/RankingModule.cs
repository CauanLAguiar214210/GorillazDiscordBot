using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Ranking;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class RankingModule : ModuleBase<SocketCommandContext>
{
    private readonly IEconomyRepository _economy;
    private readonly IRankingRepository _ranking;
    private readonly IEconomyAccessor _accessor;

    public RankingModule(IEconomyRepository economy, IRankingRepository ranking, IEconomyAccessor accessor)
    {
        _economy = economy;
        _ranking = ranking;
        _accessor = accessor;
    }

    [Command("ranking")]
    [Alias("rank")]
    public async Task RankingAsync()
    {
        var tiers = await _ranking.GetTiersAsync();
        var fame = await _ranking.GetHallOfFameAsync();
        var top = await _economy.GetTopUsersAsync(10);

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3C6 Ranking de Riqueza")
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} no servidor", Context.User.GetAvatarUrl())
            .WithDescription("O reinado começa na carteira… e termina na fama.");

        if (top.Count > 0)
        {
            var sb = new StringBuilder();
            int pos = 1;
            foreach (var u in top)
            {
                var medal = pos switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"{pos}º"
                };
                var tier = FindTier(tiers, u.NetWorth);
                var tierTag = tier != null ? $"{tier.Emoji} **{tier.Title}** " : string.Empty;
                sb.AppendLine($"{medal} {tierTag}**{await ResolveGlobalNameAsync(u.UserId, u.Username)}** — {EconomyFormat.Compact(u.NetWorth)}");
                pos++;
            }
            embed.AddField("🏅 Top Riqueza", sb.ToString());
        }
        else
        {
            embed.AddField("🏅 Top Riqueza", "📭 Ninguém tem patrimônio ainda.");
        }

        if (fame.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var f in fame)
            {
                var mainId = await _accessor.ResolveMainIdAsync(f.UserId);
                var profile = await _economy.GetOrCreateAsync(mainId, string.Empty);
                var name = await ResolveGlobalNameAsync(f.UserId, profile.Username);
                sb.AppendLine($"👑 **{f.Title}** — **{name}** — {EconomyFormat.Compact(profile.NetWorth)}");
                if (!string.IsNullOrWhiteSpace(f.Phrase))
                    sb.AppendLine($"   *“{f.Phrase}”*");
            }
            embed.AddField("🏆 Ral da Fama", sb.ToString());
        }

        embed.WithStandardFooter("Use macaco tiers para ver todas as classificações");
        await ReplyAsync(embed: embed.Build());
    }

    [Command("raldafama")]
    [Alias("fama", "hof")]
    public async Task RalDaFamaAsync()
    {
        var fame = await _ranking.GetHallOfFameAsync();

        if (fame.Count == 0)
        {
            await ReplyAsync("👑 O Ral da Fama ainda está vazio!");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F451 Ral da Fama")
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()}", Context.User.GetAvatarUrl())
            .WithDescription("As lendas que marcaram o servidor.");

        foreach (var f in fame)
        {
            var name = await ResolveGlobalNameAsync(f.UserId, string.Empty);
            var phaseLine = string.IsNullOrWhiteSpace(f.Phase) ? string.Empty : $" — {f.Phase}";
            var phrase = string.IsNullOrWhiteSpace(f.Phrase) ? string.Empty : $"\n*“{f.Phrase}”*";
            embed.AddField($"👑 {f.Title}", $"**{name}**{phaseLine}{phrase}");
        }

        embed.WithStandardFooter("As lendas nunca são esquecidas");
        await ReplyAsync(embed: embed.Build());
    }

    [Command("tiers")]
    [Alias("classificacao")]
    public async Task TiersAsync()
    {
        var tiers = await _ranking.GetTiersAsync();

        if (tiers.Count == 0)
        {
            await ReplyAsync("📋 Nenhuma classificação configurada ainda.");
            return;
        }

        var sb = new StringBuilder();
        foreach (var t in tiers)
            sb.AppendLine($"{t.Emoji} **{t.Title}** — a partir de {EconomyFormat.Full(t.MinNetWorth)} moedas");

        var embed = new EmbedBuilder()
            .WithTitle("⛰️ Classificações do Ranking")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter("Atinga o patrimônio para subir de tier");

        await ReplyAsync(embed: embed.Build());
    }

    private static RankingTier? FindTier(IReadOnlyList<RankingTier> tiers, ulong netWorth)
    {
        RankingTier? best = null;
        foreach (var tier in tiers)
        {
            if (netWorth >= tier.MinNetWorth)
                best = tier;
            else
                break;
        }
        return best;
    }

    private async Task<string> ResolveGlobalNameAsync(ulong userId, string fallback)
    {
        IUser? cached = Context.Client.GetUser(userId);
        var user = cached ?? await Context.Client.GetUserAsync(userId);
        return user?.GetDisplayName() ?? (string.IsNullOrWhiteSpace(fallback) ? $"<@{userId}>" : fallback);
    }
}