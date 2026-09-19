using Discord;
using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Release;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ReleaseAnnouncementServiceTests
{
    private const ulong ChannelId = 100;

    [Fact]
    public async Task AnnouncePendingReleasesAsync_SemReleasesPendentes_NaoConsultaServidores()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        releaseRepository
            .GetUnannouncedAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReleaseNote>());

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guild });

        announced.Should().Be(0);
        await settingsRepository.DidNotReceive().GetAsync(Arg.Any<ulong>());
    }

    [Fact]
    public async Task AnnouncePendingReleasesAsync_FalhaAoBuscarPendentes_RetornaZero()
    {
        var (service, _, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        releaseRepository
            .GetUnannouncedAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<List<ReleaseNote>>(new InvalidOperationException("boom")));

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guild });

        announced.Should().Be(0);
    }

    [Fact]
    public async Task AnnouncePendingReleasesAsync_SemServidorConfigurado_NaoMarcaRelease()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var release = CreateRelease();
        StubPending(releaseRepository, release);
        var settings = new Guild { GuildId = guild.Id };
        settingsRepository.GetAsync(guild.Id).Returns(settings);

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guild });

        announced.Should().Be(0);
        await releaseRepository.DidNotReceive().TryMarkAnnouncedAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnouncePendingReleasesAsync_CanalInexistente_NaoMarcaRelease()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var release = CreateRelease();
        StubPending(releaseRepository, release);
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);
        guild.GetTextChannelAsync(ChannelId, CacheMode.CacheOnly).Returns((ITextChannel?)null);

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guild });

        announced.Should().Be(0);
        await releaseRepository.DidNotReceive().TryMarkAnnouncedAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnouncePendingReleasesAsync_ServidorConfigurado_EnviaEMarcaRelease()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var channel = CreateChannel(guild, ChannelId);
        var release = CreateRelease();
        StubPending(releaseRepository, release);
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);
        releaseRepository
            .TryMarkAnnouncedAsync(release.Version, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guild });

        announced.Should().Be(1);
        await releaseRepository.Received(1).TryMarkAnnouncedAsync(
            release.Version, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await AssertSent(channel, 1, release.Version);
    }

    [Fact]
    public async Task AnnouncePendingReleasesAsync_ReleaseJaReivindicada_NaoEnvia()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var channel = CreateChannel(guild, ChannelId);
        var release = CreateRelease();
        StubPending(releaseRepository, release);
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);
        releaseRepository
            .TryMarkAnnouncedAsync(release.Version, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guild });

        announced.Should().Be(0);
        await AssertSent(channel, 0);
    }

    [Fact]
    public async Task AnnouncePendingReleasesAsync_FalhaEmUmServidor_NaoImpedeOsOutros()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guildA = CreateGuild(1);
        var guildB = CreateGuild(2);
        var channelA = CreateChannel(guildA, ChannelId);
        var channelB = CreateChannel(guildB, ChannelId);
        var release = CreateRelease();
        StubPending(releaseRepository, release);
        var settingsA = CreateSettings(guildA.Id, ChannelId);
        var settingsB = CreateSettings(guildB.Id, ChannelId);
        settingsRepository.GetAsync(guildA.Id).Returns(settingsA);
        settingsRepository.GetAsync(guildB.Id).Returns(settingsB);
        releaseRepository
            .TryMarkAnnouncedAsync(release.Version, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(true);
        StubSendFailure(channelA);

        var announced = await service.AnnouncePendingReleasesAsync(new[] { guildA, guildB });

        announced.Should().Be(1);
        await AssertSent(channelA, 1);
        await AssertSent(channelB, 1);
    }

    [Fact]
    public async Task AnnounceToGuildAsync_SemCanalConfigurado_Falha()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var settings = new Guild { GuildId = guild.Id };
        settingsRepository.GetAsync(guild.Id).Returns(settings);

        var result = await service.AnnounceToGuildAsync(guild, CreateRelease());

        result.Sent.Should().BeFalse();
        result.Reason.Should().Contain("release-canal");
        await releaseRepository.DidNotReceive().TryMarkAnnouncedAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceToGuildAsync_CanalInexistente_Falha()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);
        guild.GetTextChannelAsync(ChannelId, CacheMode.CacheOnly).Returns((ITextChannel?)null);

        var result = await service.AnnounceToGuildAsync(guild, CreateRelease());

        result.Sent.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
        await releaseRepository.DidNotReceive().TryMarkAnnouncedAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceToGuildAsync_ReleasePendente_EnviaEMarca()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var channel = CreateChannel(guild, ChannelId);
        var release = CreateRelease();
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);

        var result = await service.AnnounceToGuildAsync(guild, release);

        result.Sent.Should().BeTrue();
        result.Reason.Should().BeNull();
        await AssertSent(channel, 1, release.Version);
        await releaseRepository.Received(1).TryMarkAnnouncedAsync(
            release.Version, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceToGuildAsync_ReleaseJaAnunciada_NaoReclama()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var channel = CreateChannel(guild, ChannelId);
        var release = CreateRelease();
        release.AnnouncedAt = DateTime.UtcNow;
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);

        var result = await service.AnnounceToGuildAsync(guild, release);

        result.Sent.Should().BeTrue();
        await AssertSent(channel, 1, release.Version);
        await releaseRepository.DidNotReceive().TryMarkAnnouncedAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceToGuildAsync_FalhaNoEnvio_Falha()
    {
        var (service, settingsRepository, releaseRepository) = CreateService();
        var guild = CreateGuild(1);
        var channel = CreateChannel(guild, ChannelId);
        var settings = CreateSettings(guild.Id, ChannelId);
        settingsRepository.GetAsync(guild.Id).Returns(settings);
        StubSendFailure(channel);

        var result = await service.AnnounceToGuildAsync(guild, CreateRelease());

        result.Sent.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
        await releaseRepository.DidNotReceive().TryMarkAnnouncedAsync(
            Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    private static (ReleaseAnnouncementService Service, ISettingsRepository<Guild> Settings, IReleaseNoteRepository Releases)
        CreateService()
    {
        var settings = Substitute.For<ISettingsRepository<Guild>>();
        var releases = Substitute.For<IReleaseNoteRepository>();
        var service = new ReleaseAnnouncementService(
            settings,
            releases,
            NullLogger<ReleaseAnnouncementService>.Instance);
        return (service, settings, releases);
    }

    private static IGuild CreateGuild(ulong id)
    {
        var guild = Substitute.For<IGuild>();
        guild.Id.Returns(id);
        guild.Name.Returns($"Guild {id}");
        return guild;
    }

    private static ITextChannel CreateChannel(IGuild guild, ulong channelId)
    {
        var channel = Substitute.For<ITextChannel>();
        channel.Id.Returns(channelId);
        channel.Name.Returns("avisos");
        guild.GetTextChannelAsync(channelId, CacheMode.CacheOnly).Returns(channel);
        return channel;
    }

    private static Guild CreateSettings(ulong guildId, ulong channelId)
        => new()
        {
            GuildId = guildId,
            Release = new ReleaseSettings { Enabled = true, ChannelId = channelId }
        };

    private static ReleaseNote CreateRelease(string version = "v1.1.0")
        => new()
        {
            Version = version,
            Title = "Crimes, Trabalhos e Loja turbinada",
            Description = "Resumo da release",
            PublishedAt = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Features = new List<ReleaseFeature>
            {
                new() { Type = ReleaseFeatureType.New, Title = "Crimes e fiança" }
            }
        };

    private static void StubPending(IReleaseNoteRepository releases, params ReleaseNote[] items)
        => releases
            .GetUnannouncedAsync(Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(items.ToList());

    private static void StubSendFailure(ITextChannel channel)
        => channel
            .SendMessageAsync(
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Embed>(), Arg.Any<RequestOptions>(),
                Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(), Arg.Any<MessageComponent>(),
                Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(), Arg.Any<PollProperties>())
            .Returns(Task.FromException<IUserMessage>(new InvalidOperationException("boom")));

    private static Task AssertSent(ITextChannel channel, int count, string? version = null)
        => channel.Received(count).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<bool>(),
            version is null
                ? Arg.Is<Embed>(e => e != null)
                : Arg.Is<Embed>(e => e != null && (e.Title ?? string.Empty).Contains(version)),
            Arg.Any<RequestOptions>(), Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(),
            Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(),
            Arg.Any<MessageFlags>(), Arg.Any<PollProperties>());
}
