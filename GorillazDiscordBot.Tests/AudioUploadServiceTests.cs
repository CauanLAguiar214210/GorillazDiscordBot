using System.Net;
using FluentAssertions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Services;
using Lavalink4NET;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class AudioUploadServiceTests : IDisposable
{
    private const string AttachmentUrl = "https://cdn.discordapp.com/attachments/1/2/risada.mp3";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "gorillaz-upload-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("https://evil.example.com/audio.mp3")]
    [InlineData("http://cdn.discordapp.com/attachments/1/2/risada.mp3")]
    [InlineData("file:///etc/passwd")]
    public async Task Upload_OrigemForaDoDiscord_Recusar(string url)
    {
        var service = CreateService();

        var result = await service.UploadAsync(new Uri(url), "risada.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("HTTPS");
        Directory.Exists(_directory).Should().BeFalse("nada deve ser criado antes de validar a origem");
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("foto.png")]
    [InlineData("sem-extensao")]
    [InlineData(null)]
    public async Task Upload_FormatoNaoSuportado_Recusar(string? fileName)
    {
        var service = CreateService();

        var result = await service.UploadAsync(new Uri(AttachmentUrl), fileName);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Formato não suportado");
    }

    [Fact]
    public async Task Upload_ArquivoMaiorQueOLimite_RecusarSemGravar()
    {
        // 1 MB + 1 byte: passa no cabeçalho, mas estoura na leitura real.
        var bytes = new byte[(1024 * 1024) + 1];
        var service = CreateService(handler: new StubHandler(HttpStatusCode.OK, bytes), maxMegabytes: 1);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "risada.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("1 MB");
        Directory.GetFiles(_directory).Should().BeEmpty("nenhum arquivo pode sobrar de upload recusado");
    }

    [Fact]
    public async Task Upload_ContentLengthAcimaDoLimite_RecusarSemBaixar()
    {
        var handler = new StubHandler(HttpStatusCode.OK, [1, 2, 3]) { FakeContentLength = 10 * 1024 * 1024 };
        var service = CreateService(handler, maxMegabytes: 1);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "risada.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("1 MB");
    }

    [Fact]
    public async Task Upload_RespostaNaoSucesso_RecusarSemInventarMotivoDeTamanho()
    {
        var service = CreateService(new StubHandler(HttpStatusCode.NotFound, []));

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "risada.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.Error.Should().NotContain("MB", "o anexo não falhou por tamanho");
    }

    [Fact]
    public async Task Upload_AnexoVazio_Recusar()
    {
        var service = CreateService(new StubHandler(HttpStatusCode.OK, []));

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "vazio.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("vazio");
        result.Error.Should().NotContain("MB");
    }

    [Fact]
    public async Task Upload_FalhaDeRede_Recusar()
    {
        var service = CreateService(new ThrowingHandler());

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "risada.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("baixar");
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_Valido_GravaComNomeGeradoEValidadoNoNo()
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns(CreateTrack("Risada", TimeSpan.FromSeconds(5)));

        var service = CreateService(tracks: tracks);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "minha risada.mp3");

        result.Success.Should().BeTrue();
        result.Origin.Should().MatchRegex("^local:" + AudioUploadService.RelativeFolder + "/[0-9a-f]{32}\\.mp3$");
        result.TrackTitle.Should().Be("Risada");
        result.Duration.Should().Be(TimeSpan.FromSeconds(5));

        var files = Directory.GetFiles(_directory);
        files.Should().ContainSingle();
        Path.GetFileName(files[0]).Should().NotContain("minha risada", "o nome do usuário nunca vira arquivo");
        files[0].Should().EndWith(".mp3");
    }

    [Fact]
    public async Task Upload_Valido_ValidaComOArquivoJaNoNomeFinal()
    {
        // O Lavalink escolhe o codec pela extensão: se ele receber só o `.part`, responde vazio.
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns(call =>
              {
                  var identifier = call.ArgAt<string>(0);
                  var name = identifier[(identifier.LastIndexOf('/') + 1)..];
                  var exists = File.Exists(Path.Combine(_directory, name));
                  return exists ? CreateTrack("Risada", TimeSpan.FromSeconds(2)) : null;
              });

        var service = CreateService(tracks: tracks, localAudioPath: "sounds");

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "risada.mp3");

        result.Success.Should().BeTrue();
        Directory.GetFiles(_directory).Should().ContainSingle("o `.part` não pode sobrar");
    }

    [Fact]
    public async Task Upload_LavalinkNaoAbreOArquivo_NaoDeixaArquivoNoDisco()
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns((LavalinkTrack?)null);

        var service = CreateService(tracks: tracks);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "quebrado.mp3");

        result.Success.Should().BeFalse();
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Unknown title", "minha risada.mp3")]
    [InlineData("unknown TITLE", "minha risada.mp3")]
    [InlineData("   ", "minha risada.mp3")]
    [InlineData("", "minha risada.mp3")]
    [InlineData("Risada engraçada", "Risada engraçada")]
    public async Task Upload_TituloDoLavalink_TemPrecedenciaSobreONomeDoAnexo(string trackTitle, string expected)
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns(CreateTrack(trackTitle, TimeSpan.FromSeconds(3)));

        var service = CreateService(tracks: tracks);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "minha risada.mp3");

        result.TrackTitle.Should().Be(expected);
    }

    [Fact]
    public async Task Upload_Valido_UsaOCaminhoDoLavalinkComARaizLocal()
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns(CreateTrack("Alarme", TimeSpan.FromSeconds(1)));

        var service = CreateService(tracks: tracks, localAudioPath: "sounds");

        await service.UploadAsync(new Uri(AttachmentUrl), "alarme.ogg");

        await tracks.Received(1).LoadTrackAsync(
            Arg.Is<string>(id => id.StartsWith("sounds/uploads/", StringComparison.Ordinal) && id.EndsWith(".ogg", StringComparison.Ordinal)),
            Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(),
            Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_NoNaoAbreOArquivo_ApagaTemporarioERecusa()
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns((LavalinkTrack?)null);

        var service = CreateService(tracks: tracks);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "corrompido.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("não conseguiu abrir");
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_DuracaoAcimaDoLimite_ApagaTemporarioERecusa()
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns(CreateTrack("Longo", TimeSpan.FromMinutes(11)));

        var service = CreateService(tracks: tracks);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "longo.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("11min00s").And.Contain("10 min");
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_TransmissaoAoVivo_Recusa()
    {
        var tracks = Substitute.For<ITrackManager>();
        tracks.LoadTrackAsync(Arg.Any<string>(), Arg.Any<Lavalink4NET.Rest.Entities.Tracks.TrackLoadOptions>(), Arg.Any<Lavalink4NET.Rest.LavalinkApiResolutionScope>(), Arg.Any<CancellationToken>())
              .Returns(new LavalinkTrack
              {
                  Title = "Live",
                  Identifier = "x",
                  Author = "y",
                  IsLiveStream = true
              });

        var service = CreateService(tracks: tracks);

        var result = await service.UploadAsync(new Uri(AttachmentUrl), "live.mp3");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("ao vivo");
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Fact]
    public async Task Cleanup_ApagaSomenteArquivosGeradosEAntigos()
    {
        var service = CreateService();
        Directory.CreateDirectory(_directory);

        var antigoGerado = Path.Combine(_directory, $"{Guid.NewGuid():N}.mp3");
        var novoGerado = Path.Combine(_directory, $"{Guid.NewGuid():N}.ogg");
        var antigoEstranho = Path.Combine(_directory, "musica-do-admin.mp3");
        var parcialAntigo = $"{Guid.NewGuid():N}.mp3.part";

        await File.WriteAllTextAsync(antigoGerado, "a");
        await File.WriteAllTextAsync(novoGerado, "b");
        await File.WriteAllTextAsync(antigoEstranho, "c");
        await File.WriteAllTextAsync(Path.Combine(_directory, parcialAntigo), "d");

        File.SetLastWriteTimeUtc(antigoGerado, DateTime.UtcNow.AddDays(-40));
        File.SetLastWriteTimeUtc(antigoEstranho, DateTime.UtcNow.AddDays(-40));
        File.SetLastWriteTimeUtc(Path.Combine(_directory, parcialAntigo), DateTime.UtcNow.AddDays(-40));

        var removed = await service.CleanupAsync();

        removed.Should().Be(2);
        File.Exists(antigoGerado).Should().BeFalse();
        File.Exists(Path.Combine(_directory, parcialAntigo)).Should().BeFalse();
        File.Exists(novoGerado).Should().BeTrue();
        File.Exists(antigoEstranho).Should().BeTrue("arquivo que o bot não criou não é apagado");
    }

    [Fact]
    public async Task Cleanup_PastaInexistente_DevolveZero()
        => (await CreateService().CleanupAsync()).Should().Be(0);

    [Theory]
    [InlineData("som.mp3", true)]
    [InlineData("som.OGG", true)]
    [InlineData("som.wav", true)]
    [InlineData("som.flac", true)]
    [InlineData("som.m4a", true)]
    [InlineData("som.exe", false)]
    [InlineData("som", false)]
    [InlineData("../../../etc/passwd", false)]
    public void TryGetExtension_ValidaWhitelist(string fileName, bool expected)
        => AudioUploadService.TryGetExtension(fileName, out _).Should().Be(expected);

    [Theory]
    [InlineData("https://cdn.discordapp.com/a.mp3", true)]
    [InlineData("https://media.discordapp.net/a.mp3", true)]
    [InlineData("https://cdn.discord.com/a.mp3", true)]
    [InlineData("https://discordapp.com.evil.io/a.mp3", false)]
    [InlineData("https://cdn.discordapp.com.evil.io/a.mp3", false)]
    [InlineData("http://cdn.discordapp.com/a.mp3", false)]
    public void IsAllowedSource_ValidaHost(string url, bool expected)
        => AudioUploadService.IsAllowedSource(new Uri(url)).Should().Be(expected);

    [Theory]
    [InlineData(5, "5s")]
    [InlineData(90, "1min30s")]
    [InlineData(3661, "1h01min")]
    public void FormatDuration_FormataParaUsuario(int seconds, string expected)
        => AudioUploadService.FormatDuration(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    private AudioUploadService CreateService(
        HttpMessageHandler? handler = null,
        ITrackManager? tracks = null,
        int maxMegabytes = 25,
        int maxMinutes = 10,
        string localAudioPath = "sounds")
    {
        var client = new HttpClient(handler ?? new StubHandler(HttpStatusCode.OK, [0x49, 0x44, 0x33]));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AudioUploadService.HttpClientName).Returns(client);

        var audio = Substitute.For<IAudioService>();
        audio.Tracks.Returns(tracks ?? Substitute.For<ITrackManager>());

        return new AudioUploadService(
            factory,
            audio,
            Options.Create(new AudioUploadOptions { Path = _directory, MaxMegabytes = maxMegabytes, MaxMinutes = maxMinutes }),
            Options.Create(new LavalinkOptions { LocalAudioPath = localAudioPath }),
            NullLogger<AudioUploadService>.Instance);
    }

    private static LavalinkTrack CreateTrack(string title, TimeSpan duration) => new()
    {
        Title = title,
        Identifier = "x",
        Author = "y",
        Duration = duration
    };

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("conexão_resetada");
    }

    private sealed class StubHandler(HttpStatusCode statusCode, byte[] body) : HttpMessageHandler
    {
        public long? FakeContentLength { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(body);
            if (FakeContentLength.HasValue)
                content.Headers.ContentLength = FakeContentLength.Value;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = request,
                Content = content
            });
        }
    }
}
