using System.Net;
using FluentAssertions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class InstantSoundResolverTests
{
    private const string InstantUrl = "https://www.myinstants.com/instant/olha-o-macaco-5639";

    [Fact]
    public async Task ResolveAsync_ComLinkDePagina_ChamaEspelhoComOMp3DoSlug()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var resolver = CreateService(handler);

        var result = await resolver.ResolveAsync(InstantUrl);

        result.Success.Should().BeTrue();
        result.AudioUrl.Should().Be("https://myinstants.site/media/sound/olha-o-macaco.mp3");
        handler.Requests.Should().ContainSingle()
            .Which.RequestUri!.ToString().Should().Be("https://myinstants.site/media/sound/olha-o-macaco.mp3");
        handler.Requests.Single().Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task ResolveAsync_RepeteChamada_UsaCacheSemNovaRequisicao()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var resolver = CreateService(handler);

        (await resolver.ResolveAsync(InstantUrl)).Success.Should().BeTrue();
        (await resolver.ResolveAsync(InstantUrl)).Success.Should().BeTrue();

        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task ResolveAsync_QuandoEspelhoNaoTemOSom_RetornaErtoSemCachear()
    {
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var resolver = CreateService(handler);

        var first = await resolver.ResolveAsync(InstantUrl);
        var second = await resolver.ResolveAsync(InstantUrl);

        first.Success.Should().BeFalse();
        first.Error.Should().Contain("olha-o-macaco");
        second.Success.Should().BeFalse();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ResolveAsync_QuandoRedeFalha_RetornaErro()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK) { ThrowOnSend = true };
        var resolver = CreateService(handler);

        var result = await resolver.ResolveAsync(InstantUrl);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ResolveAsync_ComLinkInvalido_NaoFazRequisicao()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var resolver = CreateService(handler);

        var result = await resolver.ResolveAsync("https://www.myinstants.com/media/sounds/5639.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("inválido");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_UsaEspelhoConfiguradoPorVariavelDeAmbiente()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        var resolver = CreateService(handler, "https://espelho.test/");

        var result = await resolver.ResolveAsync(InstantUrl);

        result.AudioUrl.Should().Be("https://espelho.test/media/sound/olha-o-macaco.mp3");
    }

    private static InstantSoundResolver CreateService(RecordingHandler handler, string? mirror = null)
    {
        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(InstantSoundResolver.HttpClientName).Returns(client);

        var options = new LavalinkOptions { LocalAudioPath = "sounds" };
        if (mirror != null)
            options.InstantMirrorBaseUrl = mirror;

        return new InstantSoundResolver(
            factory,
            Options.Create(options),
            NullLogger<InstantSoundResolver>.Instance);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        public bool ThrowOnSend { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (ThrowOnSend)
                throw new HttpRequestException("sem rede");

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([0x49, 0x44, 0x33])
            });
        }
    }
}
