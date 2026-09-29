using FluentAssertions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Services;
using Lavalink4NET;
using Lavalink4NET.Players;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class AudioPlayerServiceTests
{
    private const ulong GuildId = 123;
    private const ulong ChannelId = 456;
    private const string Identifier = "local:sounds/alarme.mp3";

    private static AudioPlayerService CreateService(IAudioService audio, IInstantSoundResolver? instantSounds = null)
        => new(
            audio,
            Options.Create(new LavalinkOptions { LocalAudioPath = "sounds" }),
            instantSounds ?? Substitute.For<IInstantSoundResolver>(),
            NullLogger<AudioPlayerService>.Instance);

    private static (IAudioService Audio, IPlayerManager Players) CreateAudio()
    {
        var audio = Substitute.For<IAudioService>();
        var players = Substitute.For<IPlayerManager>();
        audio.Players.Returns(players);
        return (audio, players);
    }

    private static void ConfigureRetrieveFailing(IPlayerManager players)
    {
        players.RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                Arg.Is<ulong>(GuildId),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<LavalinkPlayer, LavalinkPlayerOptions>>(),
                Arg.Any<IOptions<LavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(PlayerResult<LavalinkPlayer>.BotNotConnected);
    }

    private static void ConfigureRetrieveThrowing(IPlayerManager players)
    {
        players.RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                Arg.Is<ulong>(GuildId),
                Arg.Any<ulong?>(),
                Arg.Any<PlayerFactory<LavalinkPlayer, LavalinkPlayerOptions>>(),
                Arg.Any<IOptions<LavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns<ValueTask<PlayerResult<LavalinkPlayer>>>(_ => throw new InvalidOperationException("boom"));
    }

    [Fact]
    public async Task Stop_SemPlayer_DeveRetornarSemAlteracoes()
    {
        var (audio, players) = CreateAudio();
        players.TryGetPlayer(Arg.Any<ulong>(), out _).Returns(false);

        var result = await CreateService(audio).StopAsync(GuildId);

        result.WasPlaying.Should().BeFalse();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Stop_ComPlayer_DevePararEDesconectar()
    {
        var (audio, players) = CreateAudio();
        var player = Substitute.For<ILavalinkPlayer>();
        players.TryGetPlayer(Arg.Any<ulong>(), out _)
            .Returns(ci =>
            {
                ci[1] = player;
                return true;
            });

        var result = await CreateService(audio).StopAsync(GuildId);

        result.WasPlaying.Should().BeTrue();
        result.Success.Should().BeTrue();
        await player.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await player.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Play_QuandoRetrieveFalha_DeveRetornarErro()
    {
        var (audio, players) = CreateAudio();
        players.TryGetPlayer(Arg.Any<ulong>(), out _).Returns(false);
        ConfigureRetrieveFailing(players);

        var result = await CreateService(audio).PlayAsync(GuildId, ChannelId, Identifier);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Play_QuandoRetrieveLancaExcecao_DeveRetornarErro()
    {
        var (audio, players) = CreateAudio();
        players.TryGetPlayer(Arg.Any<ulong>(), out _).Returns(false);
        ConfigureRetrieveThrowing(players);

        var result = await CreateService(audio).PlayAsync(GuildId, ChannelId, Identifier);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Play_ComPlayerAnterior_DevePararEDesconectarAntesDeTentar()
    {
        var (audio, players) = CreateAudio();
        var existing = Substitute.For<ILavalinkPlayer>();
        players.TryGetPlayer(Arg.Any<ulong>(), out _)
            .Returns(ci =>
            {
                ci[1] = existing;
                return true;
            });
        ConfigureRetrieveThrowing(players);

        var result = await CreateService(audio).PlayAsync(GuildId, ChannelId, Identifier);

        result.Success.Should().BeFalse();
        await existing.Received(1).StopAsync(Arg.Any<CancellationToken>());
        await existing.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_LinkDoYoutube_NaoConsultaOResolvedorDeInstantaneos()
    {
        var instantSounds = Substitute.For<IInstantSoundResolver>();
        var (audio, _) = CreateAudio();

        var result = await CreateService(audio, instantSounds)
            .ResolveAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(AudioOriginKind.YouTube);
        result.Identifier.Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        result.RequiresElevatedPermission.Should().BeFalse();
        await instantSounds.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_LinkDeSomInstantaneo_TrocaPeloMp3DoEspelho()
    {
        const string pagina = "https://www.myinstants.com/instant/olha-o-macaco-5639";
        const string mp3 = "https://myinstants.site/media/sound/olha-o-macaco.mp3";

        var instantSounds = Substitute.For<IInstantSoundResolver>();
        instantSounds.ResolveAsync(pagina, Arg.Any<CancellationToken>())
            .Returns(new InstantSoundResolveResult(true, mp3, null));

        var (audio, _) = CreateAudio();

        var result = await CreateService(audio, instantSounds).ResolveAsync(pagina);

        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(AudioOriginKind.InstantButton);
        result.Identifier.Should().Be(mp3);
        result.RequiresElevatedPermission.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_LinkDeSomInstantaneoIndisponivel_RetornaErro()
    {
        const string pagina = "https://www.myinstants.com/instant/nao-existe-999";

        var instantSounds = Substitute.For<IInstantSoundResolver>();
        instantSounds.ResolveAsync(pagina, Arg.Any<CancellationToken>())
            .Returns(new InstantSoundResolveResult(false, null, "som fora do espelho"));

        var (audio, _) = CreateAudio();

        var result = await CreateService(audio, instantSounds).ResolveAsync(pagina);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("som fora do espelho");
        result.RequiresElevatedPermission.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_UrlDiretaDeAudio_ContinuaExigindoPermissaoDeAdmin()
    {
        var (audio, _) = CreateAudio();

        var result = await CreateService(audio).ResolveAsync("https://cdn.example.com/som.mp3");

        result.IsValid.Should().BeTrue();
        result.Kind.Should().Be(AudioOriginKind.DirectUrl);
        result.RequiresElevatedPermission.Should().BeTrue();
    }
}