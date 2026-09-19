using Discord;
using GorillazDiscordBot.Domain.Entity.Release;

namespace GorillazDiscordBot.Utils;

public static class ReleaseEmbedBuilder
{
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 4000;
    private const int MaxFieldValueLength = 1000;
    private const int MaxFeaturesPerGroup = 12;

    private static readonly ReleaseFeatureType[] GroupOrder =
    {
        ReleaseFeatureType.New,
        ReleaseFeatureType.Improvement,
        ReleaseFeatureType.Fix
    };

    public static Embed BuildAnnouncement(ReleaseNote release)
    {
        var embed = new EmbedBuilder()
            .WithTitle(Truncate($"📢 Nova atualização — {release.Version}", MaxTitleLength))
            .WithGoldTheme();

        var summary = new List<string>();
        if (!string.IsNullOrWhiteSpace(release.Title))
            summary.Add($"**{release.Title}**");
        if (!string.IsNullOrWhiteSpace(release.Description))
            summary.Add(release.Description);

        var features = release.Features ?? new List<ReleaseFeature>();
        if (features.Count == 0 && summary.Count == 0)
            summary.Add("Confira as novidades desta versão!");

        if (summary.Count > 0)
            embed.WithDescription(Truncate(string.Join("\n", summary), MaxDescriptionLength));

        foreach (var (title, value) in GroupFeatures(features))
            embed.AddField(title, value, false);

        embed.WithStandardFooter($"Publicado em {FormatDate(release.PublishedAt)}");
        return embed.Build();
    }

    public static Embed BuildList(IReadOnlyList<ReleaseNote> releases, int maxItems = 10)
    {
        var embed = new EmbedBuilder()
            .WithTitle("📢 Novidades do bot")
            .WithGoldTheme();

        if (releases.Count == 0)
        {
            embed.WithDescription("Nenhuma novidade cadastrada ainda.");
            embed.WithStandardFooter("Volte depois de uma atualização!");
            return embed.Build();
        }

        var shown = Math.Min(releases.Count, maxItems);

        var lines = releases.Take(shown).Select(r =>
        {
            var pending = r.AnnouncedAt == null ? " · 📌 pendente" : string.Empty;
            var featureCount = r.Features?.Count ?? 0;
            return $"`{r.Version}` — **{r.Title}** · {FormatDate(r.PublishedAt)} · {featureCount} novidade(s){pending}";
        });

        embed.WithDescription(Truncate(string.Join("\n", lines), MaxDescriptionLength));
        embed.WithStandardFooter(shown < releases.Count
            ? $"mostrando {shown} de {releases.Count} releases · `todas` para ver tudo"
            : $"{releases.Count} {(releases.Count == 1 ? "release" : "releases")}");
        return embed.Build();
    }

    private static IEnumerable<(string Title, string Value)> GroupFeatures(List<ReleaseFeature> features)
    {
        foreach (var type in GroupOrder)
        {
            var items = features.Where(f => f.Type == type).ToList();
            if (items.Count > 0)
                yield return (GroupTitle(type), BuildFeatureList(items));
        }

        var others = features.Where(f => !GroupOrder.Contains(f.Type)).ToList();
        if (others.Count > 0)
            yield return (GroupTitle(null), BuildFeatureList(others));
    }

    private static string BuildFeatureList(List<ReleaseFeature> features)
    {
        var lines = features
            .Take(MaxFeaturesPerGroup)
            .Select(f => string.IsNullOrWhiteSpace(f.Description)
                ? $"• **{f.Title}**"
                : $"• **{f.Title}** — {f.Description}");

        var text = string.Join("\n", lines);
        if (features.Count > MaxFeaturesPerGroup)
            text += $"\n…e mais {features.Count - MaxFeaturesPerGroup}";

        return Truncate(text, MaxFieldValueLength);
    }

    private static string GroupTitle(ReleaseFeatureType? type) => type switch
    {
        ReleaseFeatureType.New => "✨ Novidades",
        ReleaseFeatureType.Improvement => "🔧 Melhorias",
        ReleaseFeatureType.Fix => "🐛 Correções",
        _ => "📌 Outros"
    };

    private static string FormatDate(DateTime utc)
        => $"<t:{new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds()}:d>";

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
