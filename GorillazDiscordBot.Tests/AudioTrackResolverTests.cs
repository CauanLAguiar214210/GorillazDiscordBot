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
        AudioTrackResolver.Resolve("ftp://cdn.example.com/som.mp3", "sounds").IsValid.Should().BeFalse();
        AudioTrackResolver.Resolve("file:///etc/passwd", "sounds").IsValid.Should().BeFalse();
        AudioTrackResolver.Resolve("javascript:alert(1)", "sounds").IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ&t=30s")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/abc123XYZ_")]
    public void LinkDoYoutube_DeveSerValidoSemPermissaoDeAdmin(string origem)
    {
        var result = AudioTrackResolver.Resolve(origem, "sounds");

        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(AudioOriginKind.YouTube);
        result.Identifier.Should().Be(origem);
        result.RequiresElevatedPermission.Should().BeFalse();
    }

    [Fact]
    public void YoutubeFalsificadoNaoPassa()
    {
        AudioTrackResolver.Resolve("https://youtube.com.evil.test/watch?v=1", "sounds")
            .Kind.Should().Be(AudioOriginKind.DirectUrl);
        AudioTrackResolver.Resolve("https://notyoutube.com/watch?v=1", "sounds")
            .Kind.Should().Be(AudioOriginKind.DirectUrl);
    }

    [Theory]
    [InlineData("ytsearch:rap do homem macaco", "ytsearch:rap do homem macaco")]
    [InlineData("YTSEARCH:macaco", "ytsearch:macaco")]
    [InlineData("ytmsearch:lá vem o homem macaco", "ytmsearch:lá vem o homem macaco")]
    [InlineData("!olha o macaco", "ytsearch:olha o macaco")]
    public void Busca_DeveVirarConsultaYouTube(string origem, string esperado)
    {
        var result = AudioTrackResolver.Resolve(origem, "sounds");

        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(AudioOriginKind.YouTubeSearch);
        result.Identifier.Should().Be(esperado);
        result.RequiresElevatedPermission.Should().BeFalse();
    }

    [Theory]
    [InlineData("ytsearch:")]
    [InlineData("ytsearch:   ")]
    [InlineData("!")]
    public void BuscaVazia_DeveSerRejeitada(string origem)
    {
        AudioTrackResolver.Resolve(origem, "sounds").IsValid.Should().BeFalse();
    }

    [Fact]
    public void BuscaMuitoLonga_DeveSerRejeitada()
    {
        AudioTrackResolver.Resolve($"ytsearch:{new string('a', 500)}", "sounds").IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://www.myinstants.com/instant/olha-o-macaco-5639", "olha-o-macaco")]
    [InlineData("https://myinstants.com/instant/serginho-mallandro-olha-o-macaco-5639", "serginho-mallandro-olha-o-macaco")]
    [InlineData("https://myinstants.site/olha-o-macaco", "olha-o-macaco")]
    [InlineData("https://www.myinstants.com/pt/instant/olha-o-macaco-5639", "olha-o-macaco")]
    public void LinkDeSomInstantaneo_DeveExtrairSlug(string origem, string slug)
    {
        AudioTrackResolver.TryGetInstantSlug(origem, out var extraido).Should().BeTrue();
        extraido.Should().Be(slug);

        var result = AudioTrackResolver.Resolve(origem, "sounds");
        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(AudioOriginKind.InstantButton);
        result.RequiresElevatedPermission.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://www.myinstants.com/")]
    [InlineData("https://www.myinstants.com/media/sounds/5639.mp3")]
    [InlineData("https://myinstants.com/instant/slug/extra/1234")]
    [InlineData("https://myinstants.site/olha%20o%20macaco")]
    [InlineData("https://myinstants.site/-macaco")]
    [InlineData("nao-e-url")]
    [InlineData("")]
    public void LinkDeSomInstantaneoInvalido_NaoDeveGerarSlug(string origem)
    {
        AudioTrackResolver.TryGetInstantSlug(origem, out var slug).Should().BeFalse();
        slug.Should().BeEmpty();
    }

    [Fact]
    public void UrlDiretaDeAudio_ExigePermissaoDeAdmin()
    {
        var result = AudioTrackResolver.Resolve("https://cdn.example.com/som.mp3", "sounds");

        result.Kind.Should().Be(AudioOriginKind.DirectUrl);
        result.RequiresElevatedPermission.Should().BeTrue();
    }

    [Fact]
    public void SomLocal_NaoExigePermissaoDeAdmin()
    {
        var result = AudioTrackResolver.Resolve("local:alarme.mp3", "sounds");

        result.Kind.Should().Be(AudioOriginKind.Local);
        result.RequiresElevatedPermission.Should().BeFalse();
    }

    [Fact]
    public void ResultadoInvalido_NuncaExigePermissao()
    {
        AudioTrackResolver.Resolve("nao-e-url", "sounds").RequiresElevatedPermission.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://youtu.be/abc", true)]
    [InlineData("ytsearch:abc", true)]
    [InlineData("ytmsearch:abc", true)]
    [InlineData("!abc", true)]
    [InlineData("https://cdn.example.com/som.mp3", false)]
    [InlineData("local:sounds/alarme.mp3", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsYouTubeRequest_IdentificaPedidosDoYoutube(string? identifier, bool esperado)
        => AudioTrackResolver.IsYouTubeRequest(identifier).Should().Be(esperado);

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