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
    private readonly IEconomyAccessor _accessor;
    private readonly IRankingRepository _ranking;
    private readonly IPatrimonioService _patrimonio;

    public RankingModule(IEconomyRepository economy, IEconomyAccessor accessor, IRankingRepository ranking, IPatrimonioService patrimonio)
    {
        _economy = economy;
        _accessor = accessor;
        _ranking = ranking;
        _patrimonio = patrimonio;
    }

    [Command("ranking")]
    [Alias("rank")]
    public async Task RankingAsync()
    {
        var fame = await _ranking.GetHallOfFameAsync();
        var top = await _patrimonio.GetTopSnapshotsAsync(10);

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3C6 Ranking de Riqueza")
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} no servidor", Context.User.GetAvatarUrl())
            .WithDescription("O reinado começa na carteira… e termina na fama.");

        if (top.Count > 0)
        {
            var sb = new StringBuilder();
            int pos = 1;
            foreach (var entry in top)
            {
                var medal = pos switch
                {
                    1 => "🥇",
                    2 => "🥈",
                    3 => "🥉",
                    _ => $"{pos}º"
                };
                var classe = ClasseEconomica.Find(entry.Snapshot.Total);
                var titulo = (classe.Title != "Miserável") ? "" : $"**{ classe.Title}**";

                sb.AppendLine($"{medal} {classe.Emoji} {titulo} **{await ResolveGlobalNameAsync(entry.UserId, entry.Username)}** — {EconomyFormat.Compact(entry.Snapshot.Total)}");
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
            sb.AppendLine();
            foreach (var f in fame)
            {
                var mainId = await _accessor.ResolveMainIdAsync(f.UserId);
                var profile = await _economy.GetOrCreateAsync(mainId, string.Empty);
                var name = await ResolveGlobalNameAsync(f.UserId, profile.Username);
                var snapshot = await _patrimonio.GetSnapshotAsync(f.UserId);
                sb.AppendLine($"👑 **{f.Title}** — **{name}**");
                if (!string.IsNullOrWhiteSpace(f.Phrase))
                    sb.AppendLine($"   *“{f.Phrase}”*");
            }
            embed.AddField("🏆 Hall da Fama", sb.ToString());
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
            await ReplyAsync("👑 O Hall da Fama ainda está vazio!");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F451 Hall da Fama")
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
        var sb = new StringBuilder();
        foreach (var c in ClasseEconomica.All)
            sb.AppendLine($"{c.Emoji} **{c.Title}** — a partir de {EconomyFormat.Full(c.MinPatrimonio)} moedas");

        var embed = new EmbedBuilder()
            .WithTitle("⛰️ Classes Econômicas")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter("Vá de Miserável até Magnata acumulando patrimônio");

        await ReplyAsync(embed: embed.Build());
    }

    private async Task<string> ResolveGlobalNameAsync(ulong userId, string fallback)
    {
        IUser? cached = Context.Client.GetUser(userId);
        var user = cached ?? await Context.Client.GetUserAsync(userId);
        return user?.GetDisplayName() ?? (string.IsNullOrWhiteSpace(fallback) ? $"<@{userId}>" : fallback);
    }
}