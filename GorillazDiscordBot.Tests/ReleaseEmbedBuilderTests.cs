using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Release;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Tests;

public class ReleaseEmbedBuilderTests
{
    [Fact]
    public void BuildAnnouncement_AgrupaFeatures_PorTipoEmOrdem()
    {
        var release = new ReleaseNote
        {
            Version = "v1.2.0",
            Title = "Grande atualização",
            PublishedAt = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Features = new List<ReleaseFeature>
            {
                new() { Type = ReleaseFeatureType.Fix, Title = "Correção A" },
                new() { Type = ReleaseFeatureType.New, Title = "Novidade A" },
                new() { Type = ReleaseFeatureType.Improvement, Title = "Melhoria A" },
            }
        };

        var embed = ReleaseEmbedBuilder.BuildAnnouncement(release);

        embed.Title.Should().Contain("v1.2.0");
        embed.Description.Should().Contain("Grande atualização");
        embed.Fields.Select(f => f.Name).Should().Equal("✨ Novidades", "🔧 Melhorias", "🐛 Correções");
        embed.Fields.ElementAt(0).Value.Should().Contain("Novidade A");
        embed.Fields.ElementAt(1).Value.Should().Contain("Melhoria A");
        embed.Fields.ElementAt(2).Value.Should().Contain("Correção A");
    }

    [Fact]
    public void BuildAnnouncement_FeatureComDescricao_IncluiDescricao()
    {
        var release = new ReleaseNote
        {
            Version = "v1.2.1",
            PublishedAt = DateTime.UtcNow,
            Features = new List<ReleaseFeature>
            {
                new() { Type = ReleaseFeatureType.New, Title = "Crimes", Description = "Roube outros jogadores" }
            }
        };

        var embed = ReleaseEmbedBuilder.BuildAnnouncement(release);

        embed.Fields.Single().Value.Should().Contain("Crimes").And.Contain("Roube outros jogadores");
    }

    [Fact]
    public void BuildAnnouncement_SemFeatures_UsaTituloEDescricaoComoFallback()
    {
        var release = new ReleaseNote
        {
            Version = "v1.0.1",
            Title = "Hotfix",
            Description = "Ajustes internos.",
            PublishedAt = DateTime.UtcNow,
        };

        var embed = ReleaseEmbedBuilder.BuildAnnouncement(release);

        embed.Fields.Should().BeEmpty();
        embed.Description.Should().Contain("Hotfix").And.Contain("Ajustes internos.");
    }

    [Fact]
    public void BuildAnnouncement_SemFeaturesNemDescricao_UsaTextoGenerico()
    {
        var release = new ReleaseNote
        {
            Version = "v1.0.2",
            Title = "",
            PublishedAt = DateTime.UtcNow,
        };

        var embed = ReleaseEmbedBuilder.BuildAnnouncement(release);

        embed.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildAnnouncement_TipoDesconhecido_VaiParaOutros()
    {
        var release = new ReleaseNote
        {
            Version = "v1.0.3",
            PublishedAt = DateTime.UtcNow,
            Features = new List<ReleaseFeature>
            {
                new() { Type = (ReleaseFeatureType)99, Title = "Misteriosa" }
            }
        };

        var embed = ReleaseEmbedBuilder.BuildAnnouncement(release);

        embed.Fields.Should().ContainSingle().Which.Name.Should().Be("📌 Outros");
    }

    [Fact]
    public void BuildAnnouncement_MuitasFeatures_LimitaTamanhoDoCampo()
    {
        var features = Enumerable.Range(1, 40)
            .Select(i => new ReleaseFeature
            {
                Type = ReleaseFeatureType.New,
                Title = $"Feature {i}",
                Description = new string('x', 100)
            })
            .ToList();

        var release = new ReleaseNote
        {
            Version = "v9.9.9",
            PublishedAt = DateTime.UtcNow,
            Features = features
        };

        var embed = ReleaseEmbedBuilder.BuildAnnouncement(release);

        var novidades = embed.Fields.Single(f => f.Name == "✨ Novidades");
        novidades.Value.Length.Should().BeLessThanOrEqualTo(1024);
        novidades.Value.Should().EndWith("…");
    }

    [Fact]
    public void BuildList_SemReleases_MostraMensagemPadrao()
    {
        var embed = ReleaseEmbedBuilder.BuildList(Array.Empty<ReleaseNote>());

        embed.Description.Should().Contain("Nenhuma novidade");
    }

    [Fact]
    public void BuildList_ComReleases_MostraVersaoTituloEPendencia()
    {
        var releases = new List<ReleaseNote>
        {
            new()
            {
                Version = "v1.1.0",
                Title = "Crimes e Trabalhos",
                PublishedAt = DateTime.UtcNow,
                AnnouncedAt = null,
                Features = new List<ReleaseFeature> { new() { Title = "Crimes" } }
            }
        };

        var embed = ReleaseEmbedBuilder.BuildList(releases);

        embed.Description.Should().Contain("v1.1.0").And.Contain("Crimes e Trabalhos").And.Contain("pendente");
    }

    [Fact]
    public void BuildList_TodasAsReleases_MostraTodasEFooterComTotal()
    {
        var releases = Enumerable.Range(1, 3)
            .Select(i => new ReleaseNote
            {
                Version = $"v1.{i}.0",
                Title = $"Release {i}",
                PublishedAt = DateTime.UtcNow,
                Features = new List<ReleaseFeature>()
            })
            .ToList();

        var embed = ReleaseEmbedBuilder.BuildList(releases, releases.Count);

        embed.Description.Should().Contain("v1.1.0").And.Contain("v1.3.0");
        embed.Footer?.Text.Should().Contain("3 releases").And.NotContain("todas");
    }

    [Fact]
    public void BuildList_ListaTruncada_FooterIndicaOpcaoTodas()
    {
        var releases = Enumerable.Range(1, 12)
            .Select(i => new ReleaseNote
            {
                Version = $"v1.{i}.0",
                PublishedAt = DateTime.UtcNow
            })
            .ToList();

        var embed = ReleaseEmbedBuilder.BuildList(releases);

        embed.Description.Should().Contain("v1.10.0").And.NotContain("v1.11.0");
        embed.Footer?.Text.Should().Contain("10 de 12").And.Contain("todas");
    }
}
