using FluentAssertions;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class FavoriteSoundMatcherTests
{
    [Fact]
    public void TryNormalizeAlias_ValidaApelidoAvulso()
    {
        FavoriteSoundMatcher.TryNormalizeAlias("  risada  ", out var alias, out var error)
            .Should().BeTrue();

        alias.Should().Be("risada");
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryNormalizeAlias_Vazio_RetornaErro(string? raw)
    {
        FavoriteSoundMatcher.TryNormalizeAlias(raw, out _, out var error)
            .Should().BeFalse();

        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryNormalizeAlias_ApelidoLongo_RetornaErro()
    {
        var raw = new string('a', FavoriteSoundMatcher.MaxAliasLength + 1);

        FavoriteSoundMatcher.TryNormalizeAlias(raw, out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("longo");
    }

    [Fact]
    public void TryNormalizeAlias_ApelidoComQuebraDeLinha_RetornaErro()
    {
        FavoriteSoundMatcher.TryNormalizeAlias("chao\nquebrado", out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("controle");
    }

    [Fact]
    public void FindBySource_IgnoraMaiusculasEEspacos()
    {
        var favorites = Build(("alarme", "https://example.com/audio.mp3"));

        FavoriteSoundMatcher.FindBySource(favorites, " HTTPS://EXAMPLE.COM/AUDIO.MP3 ")
            .Should().BeSameAs(favorites[0]);
    }

    [Fact]
    public void FindByAlias_IgnoraMaiusculasEEspacos()
    {
        var favorites = Build(("Alarme", "https://example.com/audio.mp3"));

        FavoriteSoundMatcher.FindByAlias(favorites, " alarme ")
            .Should().BeSameAs(favorites[0]);
    }

    [Fact]
    public void FindBySource_ListaVazia_DevolveNulo()
        => FavoriteSoundMatcher.FindBySource([], "qualquer").Should().BeNull();

    [Fact]
    public void FindByAlias_ListaVazia_DevolveNulo()
        => FavoriteSoundMatcher.FindByAlias([], "qualquer").Should().BeNull();

    [Fact]
    public void DisplayName_ComApelido_UsaApelido()
    {
        var favorite = new FavoriteSoundSettings { AudioSource = "local:a.mp3", Alias = "alarme" };

        FavoriteSoundMatcher.DisplayName(favorite).Should().Be("alarme");
    }

    [Fact]
    public void DisplayName_SemApelido_UsaOrigem()
    {
        var favorite = new FavoriteSoundSettings { AudioSource = "local:a.mp3" };

        FavoriteSoundMatcher.DisplayName(favorite).Should().Be("local:a.mp3");
    }

    [Fact]
    public void Truncate_NaoCortaValorCurto()
        => FavoriteSoundMatcher.Truncate("abc", 10).Should().Be("abc");

    [Fact]
    public void Truncate_CortaComReticencias()
        => FavoriteSoundMatcher.Truncate("abcdefghij", 5).Should().Be("abcd…");

    private static List<FavoriteSoundSettings> Build(params (string Alias, string Source)[] items)
        => items.Select(i => new FavoriteSoundSettings { Alias = i.Alias, AudioSource = i.Source }).ToList();
}
