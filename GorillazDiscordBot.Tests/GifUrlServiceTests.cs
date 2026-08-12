using System.Net;
using FluentAssertions;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Tests;

public class GifUrlServiceTests
{
    [Theory]
    [InlineData("https://example.com/image.gif")]
    [InlineData("https://example.com/foto.png")]
    [InlineData("https://example.com/foto.jpg")]
    [InlineData("https://example.com/foto.jpeg")]
    [InlineData("https://example.com/foto.webp")]
    public async Task GetDirectUrlAsync_ComExtensaoDeImagem_RetornaUrl(string url)
    {
        var service = CreateService("<html></html>");

        var result = await service.GetDirectUrlAsync(url);

        result.Should().Be(url);
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComExtensaoMaiuscula_RetornaUrl()
    {
        var service = CreateService("<html></html>");

        var result = await service.GetDirectUrlAsync("https://example.com/IMG.GIF");

        result.Should().Be("https://example.com/IMG.GIF");
    }

    [Theory]
    [InlineData("https://c.tenor.com/abc123.gif")]
    [InlineData("https://media.tenor.com/abc123.webp")]
    public async Task GetDirectUrlAsync_ComHostDiretoDoTenor_RetornaUrl(string url)
    {
        var service = CreateService("<html></html>");

        var result = await service.GetDirectUrlAsync(url);

        result.Should().Be(url);
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComPaginaDoTenorEOgImage_ExtraiGif()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="https://media.tenor.com/gifs/abc123.gif" />
              </head>
            </html>
            """;
        var service = CreateService(html);

        var result = await service.GetDirectUrlAsync("https://tenor.com/view/meu-gif-123");

        result.Should().Be("https://media.tenor.com/gifs/abc123.gif");
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComPaginaDoTenorSemOgImage_UsaFallbackCdn()
    {
        var html = """
            <html>
              <body>
                <img src="https://c.tenor.com/xyz789.gif" alt="meu gif" />
              </body>
            </html>
            """;
        var service = CreateService(html);

        var result = await service.GetDirectUrlAsync("https://tenor.com/view/meu-gif-456");

        result.Should().Be("https://c.tenor.com/xyz789.gif");
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComOgImageRelativa_CaiNoFallbackCdn()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="/images/abc.gif" />
              </head>
              <body>
                <img src="https://c.tenor.com/xyz789.gif" />
              </body>
            </html>
            """;
        var service = CreateService(html);

        var result = await service.GetDirectUrlAsync("https://tenor.com/view/meu-gif-456");

        result.Should().Be("https://c.tenor.com/xyz789.gif");
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComUrlVazia_LancaArgumentException()
    {
        var service = CreateService("<html></html>");

        var act = async () => await service.GetDirectUrlAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComUrlMalformada_LancaInvalidOperationException()
    {
        var service = CreateService("<html></html>");

        var act = async () => await service.GetDirectUrlAsync("nao-e-uma-url");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComUrlNaoSuportada_LancaInvalidOperationException()
    {
        var service = CreateService("<html></html>");

        var act = async () => await service.GetDirectUrlAsync("https://www.youtube.com/watch?v=abc");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetDirectUrlAsync_ComPaginaDoTenorSemGif_LancaInvalidOperationException()
    {
        var service = CreateService("<html><body><p>sem gif</p></body></html>");

        var act = async () => await service.GetDirectUrlAsync("https://tenor.com/view/meu-gif-789");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static IGifUrlService CreateService(string responseBody)
    {
        var handler = new StubHandler(responseBody);
        return new GifUrlService(new HttpClient(handler));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StubHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody)
            };
            return Task.FromResult(response);
        }
    }
}
