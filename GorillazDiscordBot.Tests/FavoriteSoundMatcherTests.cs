using FluentAssertions;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class FavoriteSoundMatcherTests
{
    [Fact]
    public void TryParseInput_SemSeparador_KeepsWholeOrigin()
    {
        FavoriteSoundMatcher.TryParseInput("https://youtu.be/hChdmFBjH3s", out var origin, out var alias, out var error)
            .Should().BeTrue();

        origin.Should().Be("https://youtu.be/hChdmFBjH3s");
        alias.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_ComSeparador_SepararOrigemEApelido()
    {
        FavoriteSoundMatcher.TryParseInput("ytsearch:bonde do macaco = bonde", out var origin, out var alias, out var error)
            .Should().BeTrue();

        origin.Should().Be("ytsearch:bonde do macaco");
        alias.Should().Be("bonde");
        error.Should().BeNull();
    }

    [Fact]
    public void TryParseInput_UsaOUltimoSeparador_PermiteigualNoTermo()
    {
        FavoriteSoundMatcher.TryParseInput("ytsearch:a = b = final", out var origin, out var alias, out _)
            .Should().BeTrue();

        origin.Should().Be("ytsearch:a = b");
        alias.Should().Be("final");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("= apelido")]
    [InlineData("origem = ")]
    public void TryParseInput_EntradaInvalida_RetornaErro(string input)
    {
        FavoriteSoundMatcher.TryParseInput(input, out _, out _, out var error)
            .Should().BeFalse();

        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryParseInput_ApelidoLongo_RetornaErro()
    {
        var input = "origem = " + new string('a', FavoriteSoundMatcher.MaxAliasLength + 1);

        FavoriteSoundMatcher.TryParseInput(input, out _, out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("longo");
    }

    [Fact]
    public void TryParseInput_ApelidoComQuebraDeLinha_RetornaErro()
    {
        FavoriteSoundMatcher.TryParseInput("origem = chao\nquebrado", out _, out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("controle");
    }

    [Fact]
    public void TryNormalizeAlias_ValidaApelidoAvulso()
    {
        FavoriteSoundMatcher.TryNormalizeAlias("  risada  ", out var alias, out var error)
            .Should().BeTrue();

        alias.Should().Be("risada");
        error.Should().BeNull();
    }

    [Fact]
    public void TryFind_PorIndice_UmBased()
    {
        var favorites = Build(("primeiro", "a"), ("segundo", "b"));

        FavoriteSoundMatcher.TryFind(favorites, "2", out var favorite, out _).Should().BeTrue();

        favorite!.AudioSource.Should().Be("b");
    }

    [Fact]
    public void TryFind_IndiceForaDoIntervalo_RetornaErro()
    {
        var favorites = Build(("primeiro", "a"));

        FavoriteSoundMatcher.TryFind(favorites, "5", out var favorite, out var error)
            .Should().BeFalse();

        favorite.Should().BeNull();
        error.Should().Contain("Não existe o favorito **5**");
    }

    [Fact]
    public void TryFind_PorApelido_IgnoraMaiusculas()
    {
        var favorites = new List<FavoriteSoundSettings>
        {
            new() { AudioSource = "https://youtu.be/abc", Alias = "Alarme" }
        };

        FavoriteSoundMatcher.TryFind(favorites, "alarme", out var favorite, out _)
            .Should().BeTrue();

        favorite!.AudioSource.Should().Be("https://youtu.be/abc");
    }

    [Fact]
    public void TryFind_PorPrefixoDeId()
    {
        var favorites = new List<FavoriteSoundSettings>
        {
            new() { Id = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789"), AudioSource = "a" }
        };
        var prefix = favorites[0].Id.ToString()[..6];

        FavoriteSoundMatcher.TryFind(favorites, prefix, out var favorite, out _)
            .Should().BeTrue();

        favorite.Should().BeSameAs(favorites[0]);
    }

    [Fact]
    public void TryFind_PorOrigem()
    {
        var favorites = Build(("unico", "local:alarme.mp3"));

        FavoriteSoundMatcher.TryFind(favorites, "local:alarme.mp3", out var favorite, out _)
            .Should().BeTrue();

        favorite.Should().BeSameAs(favorites[0]);
    }

    [Fact]
    public void TryFind_ListaVazia_RetornaErro()
    {
        FavoriteSoundMatcher.TryFind([], "1", out var favorite, out var error)
            .Should().BeFalse();

        favorite.Should().BeNull();
        error.Should().Contain("Nenhum favorito ainda");
    }

    [Fact]
    public void TryFind_ChaveDesconhecida_RetornaErro()
    {
        var favorites = Build(("unico", "a"));

        FavoriteSoundMatcher.TryFind(favorites, "inexistente", out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("inexistente");
    }

    [Theory]
    [InlineData("aleatorio", true)]
    [InlineData("ALEATORIO", true)]
    [InlineData(" aleatorio ", true)]
    [InlineData("1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRandomRequest(string? key, bool expected)
        => FavoriteSoundMatcher.IsRandomRequest(key).Should().Be(expected);

    [Fact]
    public void PickRandom_ListaVazia_DevolveNulo()
        => FavoriteSoundMatcher.PickRandom([]).Should().BeNull();

    [Fact]
    public void PickRandom_ListaComItens_DevolveUmDeles()
    {
        var favorites = Build(("a", "1"), ("b", "2"), ("c", "3"));

        var picked = FavoriteSoundMatcher.PickRandom(favorites);

        picked.Should().NotBeNull();
        favorites.Should().Contain(picked!);
    }

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
    public void IndexOf_RetornaPosicaoUmBased()
    {
        var favorites = Build(("a", "1"), ("b", "2"));

        FavoriteSoundMatcher.IndexOf(favorites, favorites[1]).Should().Be(2);
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
