using FluentAssertions;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Tests;

public class MyInstantsResolverTests
{
    [Theory]
    [InlineData("https://www.myinstants.com/pt/instant/bom-dia-minha-princesa-60981/", true)]
    [InlineData("https://www.myinstants.com/en/instant/some-sound/", true)]
    [InlineData("https://myinstants.com/instant/foo-bar/", true)]
    [InlineData("https://www.myinstants.com/pt/instant/bom-dia-minha-princesa-60981/?utm_source=copy&utm_medium=share", true)]
    [InlineData("https://www.myinstants.com/media/sounds/bom-dia-minha-princesa.mp3", false)]
    [InlineData("https://www.myinstants.com/pt/index.html", false)]
    [InlineData("https://www.myinstants.com/pt/instant/foo/embed/", false)]
    [InlineData("https://example.com/instant/foo/", false)]
    [InlineData("https://example.com/media/sounds/a.mp3", false)]
    public void IsPageUrl_DetectaSomentePaginasDeInstantDoMyInstants(string url, bool esperado)
    {
        var uri = new Uri(url);

        var resultado = MyInstantsResolver.IsPageUrl(uri);

        resultado.Should().Be(esperado);
    }

    [Fact]
    public void ExtractAudioUrl_ComMetaOgAudio_RetornaUrlAbsoluta()
    {
        var html = """
            <html><head>
            <meta property="og:title" content="Bom dia minha princesa - Botão sonoro"/>
            <meta property="og:audio" content="https://www.myinstants.com/media/sounds/bom-dia-minha-princesa.mp3"/>
            <meta property="og:audio:type" content="audio/mpeg" />
            </head></html>
            """;
        var page = new Uri("https://www.myinstants.com/pt/instant/bom-dia-minha-princesa-60981/");

        var audio = MyInstantsResolver.ExtractAudioUrl(html, page);

        audio.Should().NotBeNull();
        audio!.AbsoluteUri.Should().Be("https://www.myinstants.com/media/sounds/bom-dia-minha-princesa.mp3");
    }

    [Fact]
    public void ExtractAudioUrl_ComDataUrlRelativo_ResolveContraBaseDaPagina()
    {
        var html = """
            <button id="instant-page-button-element"
                data-url="/media/sounds/bom-dia-minha-princesa.mp3"
                onclick="play('/media/sounds/bom-dia-minha-princesa.mp3')"></button>
            """;
        var page = new Uri("https://www.myinstants.com/pt/instant/bom-dia-minha-princesa-60981/");

        var audio = MyInstantsResolver.ExtractAudioUrl(html, page);

        audio.Should().NotBeNull();
        audio!.AbsoluteUri.Should().Be("https://www.myinstants.com/media/sounds/bom-dia-minha-princesa.mp3");
    }

    [Fact]
    public void ExtractAudioUrl_ComHtmlRealDaPagina_ExtraiMp3()
    {
        var html = """
            <!DOCTYPE html>
            <html lang="pt">
              <head>
                <script>
                  var preloadAudioUrl = '/media/sounds/bom-dia-minha-princesa.mp3';
                </script>
                <title>Bom dia minha princesa - Botão de efeito sonoro instantâneo | Myinstants</title>
                <meta property="og:audio" content="https://www.myinstants.com/media/sounds/bom-dia-minha-princesa.mp3"/>
                <meta property="og:audio:type" content="audio/mpeg" />
              </head>
              <body>
                <button id="instant-page-button-element" data-url="/media/sounds/bom-dia-minha-princesa.mp3"></button>
              </body>
            </html>
            """;
        var page = new Uri("https://www.myinstants.com/pt/instant/bom-dia-minha-princesa-60981/?utm_source=copy&utm_medium=share");

        var audio = MyInstantsResolver.ExtractAudioUrl(html, page);

        audio.Should().NotBeNull();
        audio!.AbsolutePath.Should().EndWith(".mp3");
    }

    [Fact]
    public void ExtractAudioUrl_SemAudioNoHtml_RetornaNull()
    {
        var html = "<html><head><title>Sem som</title></head><body>vazio</body></html>";
        var page = new Uri("https://www.myinstants.com/pt/instant/inexistente-000/");

        var audio = MyInstantsResolver.ExtractAudioUrl(html, page);

        audio.Should().BeNull();
    }
}
