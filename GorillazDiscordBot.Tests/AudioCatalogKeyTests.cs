using FluentAssertions;
using GorillazDiscordBot.Commands.Audio;
using Xunit;

namespace GorillazDiscordBot.Tests;

public class AudioCatalogKeyTests
{
    [Fact]
    public void CatalogKey_EhEstavelParaMesmaSourceIgnorandoMaiusculas()
    {
        var a = AudioSlashModule.CatalogKey("https://exemplo.com/Musica.MP3");
        var b = AudioSlashModule.CatalogKey("https://exemplo.com/musica.mp3");

        a.Should().Be(b);
    }

    [Fact]
    public void CatalogKey_DiferenciaSourcesDiferentes()
    {
        var a = AudioSlashModule.CatalogKey("local:sounds/a.mp3");
        var b = AudioSlashModule.CatalogKey("local:sounds/b.mp3");

        a.Should().NotBe(b);
    }

    [Fact]
    public void CatalogKey_TemFormatoDe16HexMinusculas()
    {
        var key = AudioSlashModule.CatalogKey("local:sounds/alo.mp3");

        key.Should().HaveLength(16);
        key.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void CatalogKey_CabeNoCustomIdDeComponente()
    {
        var key = AudioSlashModule.CatalogKey("local:sounds/muito-longo-com-caracteres-especiais-ãçõé.mp3");
        var customId = $"audio-catalog:action:nonlocal:99:{key}:unfavorite";

        customId.Length.Should().BeLessOrEqualTo(100);
    }
}
