using FluentAssertions;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class AudioTrackResolverTests
{
    [Fact]
    public void UrlHttps_PermaneceValida()
    {
        var result = AudioTrackResolver.Resolve("https://cdn.example.com/som.mp3", "sounds");

        result.IsValid.Should().BeTrue();
        result.Identifier.Should().Be("https://cdn.example.com/som.mp3");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void UrlHttp_PermaneceValida()
    {
        var result = AudioTrackResolver.Resolve("http://cdn.example.com/som.mp3", "sounds");

        result.IsValid.Should().BeTrue();
        result.Identifier.Should().Be("http://cdn.example.com/som.mp3");
    }

    [Fact]
    public void LocalSimples_DeveMontarIdentifierComRoot()
    {
        var result = AudioTrackResolver.Resolve("local:alarme.mp3", "sounds");

        result.IsValid.Should().BeTrue();
        result.Identifier.Should().Be("local:sounds/alarme.mp3");
    }

    [Fact]
    public void LocalComRootCustomizado_UsaRootInformado()
    {
        var result = AudioTrackResolver.Resolve("local:sirene.mp3", "audio");

        result.IsValid.Should().BeTrue();
        result.Identifier.Should().Be("local:audio/sirene.mp3");
    }

    [Fact]
    public void LocalComSubpasta_DeveManterCaminhoRelativo()
    {
        var result = AudioTrackResolver.Resolve("local:nino/som.mp3", "sounds");

        result.IsValid.Should().BeTrue();
        result.Identifier.Should().Be("local:sounds/nino/som.mp3");
    }

    [Fact]
    public void LocalComTraversal_DeveSerRejeitado()
    {
        var result = AudioTrackResolver.Resolve("local:../../etc/passwd", "sounds");

        result.IsValid.Should().BeFalse();
        result.Identifier.Should().BeNull();
    }

    [Fact]
    public void LocalAbsoluto_DeveSerRejeitado()
    {
        var result = AudioTrackResolver.Resolve("local:/etc/passwd", "sounds");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LocalComEspacosOuCaracteresInvalidos_DeveSerRejeitado()
    {
        var result = AudioTrackResolver.Resolve("local:som com espaço.mp3", "sounds");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LocalVazio_DeveSerRejeitado()
    {
        AudioTrackResolver.Resolve("local:", "sounds").IsValid.Should().BeFalse();
        AudioTrackResolver.Resolve("local:   ", "sounds").IsValid.Should().BeFalse();
    }

    [Fact]
    public void OrigemVazia_DeveSerRejeitada()
    {
        AudioTrackResolver.Resolve("", "sounds").IsValid.Should().BeFalse();
        AudioTrackResolver.Resolve(null, "sounds").IsValid.Should().BeFalse();
    }

    [Fact]
    public void OutrosEsquemas_DeveSerRejeitado()
    {
        var result = AudioTrackResolver.Resolve("ytsearch:qualquer coisa", "sounds");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RootVazioOuNulo_UsaPadraoSounds()
    {
        AudioTrackResolver.Resolve("local:alarme.mp3", "").Identifier.Should().Be("local:sounds/alarme.mp3");
        AudioTrackResolver.Resolve("local:alarme.mp3", null).Identifier.Should().Be("local:sounds/alarme.mp3");
    }

    [Fact]
    public void RootAbsolutoOuComCaracteresInvalidos_MantemRootPadrao()
    {
        AudioTrackResolver.Resolve("local:alarme.mp3", "/etc").Identifier.Should().Be("local:sounds/alarme.mp3");
        AudioTrackResolver.Resolve("local:alarme.mp3", "não é root").Identifier.Should().Be("local:sounds/alarme.mp3");
    }

    [Fact]
    public void ToServerIdentifier_ComPrefixoLocal_RemovePrefixo()
    {
        AudioTrackResolver.ToServerIdentifier("local:sounds/oleodemacaco.mp3")
            .Should().Be("sounds/oleodemacaco.mp3");
    }

    [Fact]
    public void ToServerIdentifier_SemPrefixoLocal_MantemIdentifier()
    {
        AudioTrackResolver.ToServerIdentifier("https://cdn.example.com/som.mp3")
            .Should().Be("https://cdn.example.com/som.mp3");
    }

    [Fact]
    public void IsRemoteUrl_HttpsEhRemota()
    {
        AudioTrackResolver.IsRemoteUrl("https://cdn.example.com/som.mp3").Should().BeTrue();
        AudioTrackResolver.IsRemoteUrl("http://cdn.example.com/som.mp3").Should().BeTrue();
        AudioTrackResolver.IsRemoteUrl("HTTPS://CDN.EXAMPLE.COM/SOM.MP3").Should().BeTrue();
    }

    [Fact]
    public void IsRemoteUrl_LocalEhNull_NaoSaoRemotas()
    {
        AudioTrackResolver.IsRemoteUrl("local:sounds/alarme.mp3").Should().BeFalse();
        AudioTrackResolver.IsRemoteUrl("sounds/alarme.mp3").Should().BeFalse();
        AudioTrackResolver.IsRemoteUrl(null).Should().BeFalse();
        AudioTrackResolver.IsRemoteUrl("").Should().BeFalse();
    }
}