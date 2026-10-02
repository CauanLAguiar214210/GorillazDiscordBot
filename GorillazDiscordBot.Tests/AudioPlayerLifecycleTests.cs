using System.Collections.Immutable;
using FluentAssertions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Services;
using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Protocol.Models.Filters;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;

namespace GorillazDiscordBot.Tests;

public class AudioPlayerLifecycleTests
{
    private const ulong GuildId = 999;
    private const ulong ChannelId = 888;
    private const string Identifier = "local:sounds/alarme.mp3";

    [Fact]
    public async Task Play_QuandoFaixaComeca_DeveRetornarSucesso()
    {
        var fixture = new Fixture();
        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);

        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.StartedAsync(fixture.Audio, new TrackStartedEventArgs(fixture.Current.Player, CreateTrack()));

        var result = await playTask;

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task Play_QuandoNoLancaExcecaoNaFaixa_DeveReportarDetalheDoNo()
    {
        var fixture = new Fixture();
        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);

        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.ExceptionAsync(
            fixture.Audio,
            new TrackExceptionEventArgs(
                fixture.Current.Player,
                CreateTrack(),
                new TrackException(ExceptionSeverity.Common, "Unable to find video", "yt-dlp returned 1")));

        var result = await playTask;

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unable to find video");
    }

    [Fact]
    public async Task Play_QuandoYouTubeFalha_DeveCitarVerificacaoDoPlugin()
    {
        var fixture = new Fixture();
        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, "https://youtu.be/hChdmFBjH3s");

        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.ExceptionAsync(
            fixture.Audio,
            new TrackExceptionEventArgs(fixture.Current.Player, CreateTrack(), new TrackException(ExceptionSeverity.Common, "boom", null)));

        var result = await playTask;

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("plugins.youtube.enabled");
    }

    [Fact]
    public async Task Play_QuandoFaixaNuncaComeca_DeveReportarTimeout()
    {
        var fixture = new Fixture(startTimeoutSeconds: 1);

        var result = await fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("1s");
    }

    [Fact]
    public async Task Play_QuandoPlayerFoiReciclado_TentaNovamenteComNovoPlayer()
    {
        var fixture = new Fixture();
        var recycled = CreatePlayer();
        await recycled.Player.DisposeAsync();

        fixture.Players
            .RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong>(),
                Arg.Any<PlayerFactory<LavalinkPlayer, LavalinkPlayerOptions>>(),
                Arg.Any<IOptions<LavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(
                PlayerResult<LavalinkPlayer>.Success(recycled.Player),
                PlayerResult<LavalinkPlayer>.Success(fixture.Current.Player));

        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);

        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.StartedAsync(fixture.Audio, new TrackStartedEventArgs(fixture.Current.Player, CreateTrack()));

        var result = await playTask;

        result.Success.Should().BeTrue();
        var attemptCount = fixture.Players
            .Received(2)
            .RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong>(),
                Arg.Any<PlayerFactory<LavalinkPlayer, LavalinkPlayerOptions>>(),
                Arg.Any<IOptions<LavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>());

        await attemptCount;
    }

    [Fact]
    public async Task TrackEnded_DoPlayerAnterior_NaoDesconectaAPlaybackAtual()
    {
        var fixture = new Fixture();
        var previousSession = CreatePlayer();

        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);
        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.StartedAsync(fixture.Audio, new TrackStartedEventArgs(fixture.Current.Player, CreateTrack()));
        (await playTask).Success.Should().BeTrue();

        // Evento atrasado da sessão anterior: não pode derrubar o áudio atual.
        await fixture.Events.EndedAsync(fixture.Audio, new TrackEndedEventArgs(previousSession.Player, CreateTrack(), TrackEndReason.Finished));

        await fixture.Current.Discord.DidNotReceiveWithAnyArgs().SendVoiceUpdateAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task TrackEnded_DoPlayerAtual_DesconectaParaTocaESai()
    {
        var fixture = new Fixture();

        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);
        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.StartedAsync(fixture.Audio, new TrackStartedEventArgs(fixture.Current.Player, CreateTrack()));
        (await playTask).Success.Should().BeTrue();

        await fixture.Events.EndedAsync(fixture.Audio, new TrackEndedEventArgs(fixture.Current.Player, CreateTrack(), TrackEndReason.Finished));

        await fixture.Current.Discord.Received(1).SendVoiceUpdateAsync(
            GuildId,
            null,
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Join_TrackEnded_NaoDesconectaSessaoPersistente()
    {
        var fixture = new Fixture();

        var joined = await fixture.Service.JoinAsync(GuildId, ChannelId);

        joined.Success.Should().BeTrue();
        await fixture.Current.Discord.DidNotReceiveWithAnyArgs().SendVoiceUpdateAsync(default, default, default, default, default);

        var playTask = fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);
        await fixture.WaitForPlayRequestAsync();
        await fixture.Events.StartedAsync(fixture.Audio, new TrackStartedEventArgs(fixture.Current.Player, CreateTrack()));
        (await playTask).Success.Should().BeTrue();

        await fixture.Events.EndedAsync(fixture.Audio, new TrackEndedEventArgs(fixture.Current.Player, CreateTrack(), TrackEndReason.Finished));

        await fixture.Current.Discord.DidNotReceiveWithAnyArgs().SendVoiceUpdateAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task Play_QuandoPlayerRecuperadoEhReciclado_NaoAcusaPluginDoYouTube()
    {
        var fixture = new Fixture();
        fixture.Players
            .RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                Arg.Any<ulong>(),
                Arg.Any<ulong>(),
                Arg.Any<PlayerFactory<LavalinkPlayer, LavalinkPlayerOptions>>(),
                Arg.Any<IOptions<LavalinkPlayerOptions>>(),
                Arg.Any<PlayerRetrieveOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(RecycledPlayer());

        var result = await fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("reciclado");
        result.Error.Should().NotContain("plugin");
    }

    [Fact]
    public async Task Play_QuandoFalhaNoInicio_DesconectaParaNaoFicarSilencioso()
    {
        var fixture = new Fixture(startTimeoutSeconds: 1);

        var result = await fixture.Service.PlayAsync(GuildId, ChannelId, Identifier);

        result.Success.Should().BeFalse();
        await fixture.Current.Discord.Received(1).SendVoiceUpdateAsync(
            GuildId,
            null,
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    private static Func<CallInfo, PlayerResult<LavalinkPlayer>> RecycledPlayer()
        => _ => throw new ObjectDisposedException("LavalinkPlayer");

    private static LavalinkTrack CreateTrack() => new()
    {
        Title = "Alarme",
        Identifier = Identifier,
        Author = "Teste",
        Duration = TimeSpan.FromSeconds(3)
    };

    private static TestPlayer CreatePlayer()
    {
        var api = Substitute.For<ILavalinkApiClient>();
        var lifecycle = Substitute.For<IPlayerLifecycle>();
        var discord = Substitute.For<IDiscordClientWrapper>();
        var state = new PlayerInformationModel(
            GuildId,
            CurrentTrack: null,
            Volume: 1f,
            IsPaused: false,
            VoiceState: new VoiceStateModel("token", "smart.local", "voice-session"),
            Filters: new PlayerFilterMapModel());

        api.UpdatePlayerAsync(
                Arg.Any<string>(),
                Arg.Any<ulong>(),
                Arg.Any<PlayerUpdateProperties>(),
                Arg.Any<CancellationToken>())
            .Returns(state);

        var properties = Substitute.For<IPlayerProperties<LavalinkPlayer, LavalinkPlayerOptions>>();
        properties.ApiClient.Returns(api);
        properties.DiscordClient.Returns(discord);
        properties.InitialState.Returns(state);
        properties.InitialTrack.Returns((ITrackQueueItem?)null);
        properties.Label.Returns("test");
        properties.Logger.Returns(NullLogger<LavalinkPlayer>.Instance);
        properties.SystemClock.Returns(Substitute.For<ISystemClock>());
        properties.Options.Returns(Options.Create(new LavalinkPlayerOptions()));
        properties.ServiceProvider.Returns((IServiceProvider?)null);
        properties.VoiceChannelId.Returns(ChannelId);
        properties.SessionId.Returns("session-1");
        properties.Lifecycle.Returns(lifecycle);

        return new TestPlayer(new LavalinkPlayer(properties), api, discord);
    }

    private sealed record TestPlayer(LavalinkPlayer Player, ILavalinkApiClient Api, IDiscordClientWrapper Discord);

    private sealed class CapturedEvents
    {
        public AsyncEventHandler<TrackStartedEventArgs>? Started { get; set; }

        public AsyncEventHandler<TrackExceptionEventArgs>? Exception { get; set; }

        public AsyncEventHandler<TrackEndedEventArgs>? Ended { get; set; }

        public Task StartedAsync(object sender, TrackStartedEventArgs args) => Required(Started)(sender, args);

        public Task ExceptionAsync(object sender, TrackExceptionEventArgs args) => Required(Exception)(sender, args);

        public Task EndedAsync(object sender, TrackEndedEventArgs args) => Required(Ended)(sender, args);

        private static AsyncEventHandler<T> Required<T>(AsyncEventHandler<T>? handler) where T : EventArgs
            => handler ?? throw new InvalidOperationException($"Handler de {typeof(T).Name} não capturado.");
    }

    private sealed class Fixture
    {
        public Fixture(int startTimeoutSeconds = 15)
        {
            Audio = Substitute.For<IAudioService>();
            Players = Substitute.For<IPlayerManager>();
            Events = new CapturedEvents();

            Audio.Players.Returns(Players);
            Audio.When(x => x.TrackStarted += Arg.Any<AsyncEventHandler<TrackStartedEventArgs>>())
                .Do(call => Events.Started = call.Arg<AsyncEventHandler<TrackStartedEventArgs>>());
            Audio.When(x => x.TrackException += Arg.Any<AsyncEventHandler<TrackExceptionEventArgs>>())
                .Do(call => Events.Exception = call.Arg<AsyncEventHandler<TrackExceptionEventArgs>>());
            Audio.When(x => x.TrackEnded += Arg.Any<AsyncEventHandler<TrackEndedEventArgs>>())
                .Do(call => Events.Ended = call.Arg<AsyncEventHandler<TrackEndedEventArgs>>());

            Current = CreatePlayer();
            Players.TryGetPlayer(GuildId, out var ignoredPlayer).Returns(false);
            Players
                .RetrieveAsync<LavalinkPlayer, LavalinkPlayerOptions>(
                    Arg.Any<ulong>(),
                    Arg.Any<ulong>(),
                    Arg.Any<PlayerFactory<LavalinkPlayer, LavalinkPlayerOptions>>(),
                    Arg.Any<IOptions<LavalinkPlayerOptions>>(),
                    Arg.Any<PlayerRetrieveOptions>(),
                    Arg.Any<CancellationToken>())
                .Returns(PlayerResult<LavalinkPlayer>.Success(Current.Player));

            Service = new AudioPlayerService(
                Audio,
                Options.Create(new LavalinkOptions { PlaybackStartTimeoutSeconds = startTimeoutSeconds }),
                Substitute.For<IInstantSoundResolver>(),
                Logger);
        }

        public Microsoft.Extensions.Logging.ILogger<AudioPlayerService> Logger { get; } = Substitute.For<Microsoft.Extensions.Logging.ILogger<AudioPlayerService>>();

        public IAudioService Audio { get; }

        public IPlayerManager Players { get; }

        public CapturedEvents Events { get; }

        public TestPlayer Current { get; }

        public AudioPlayerService Service { get; }

        /// <summary>
        /// Aguarda o PATCH de play no nó: nesse ponto a pendência de início já está
        /// registrada, então o evento de <c>TrackStarted</c> não é perdido.
        /// </summary>
        public async Task WaitForPlayRequestAsync()
        {
            var deadline = Environment.TickCount64 + 5000;
            while (PlayPatchCount() == 0)
            {
                if (Environment.TickCount64 > deadline)
                    throw new TimeoutException("O player não recebeu a requisição de play.");

                await Task.Delay(10);
            }

            // Garante que o registro da pendência terminou antes do evento.
            await Task.Yield();
            await Task.Delay(20);
        }

        public int PlayPatchCount() => Current.Api.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(ILavalinkApiClient.UpdatePlayerAsync));
    }
}
