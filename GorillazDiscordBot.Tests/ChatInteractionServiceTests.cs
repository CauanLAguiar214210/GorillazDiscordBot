using System.Net;
using Discord;
using FluentAssertions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ChatInteractionServiceTests
{
    private const string MediaUrl = "https://cdn.example.com/midia.mp3";
    private static readonly byte[] Payload = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task Texto_DeveEnviarMensagemSimplesSemAnexo()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var interaction = CreateInteraction(GuildInteractionType.Texto, "olá, mundo");

        await service.SendResponseAsync(channel, interaction);

        await channel.Received(1).SendMessageAsync(
            "olá, mundo", Arg.Any<bool>(), Arg.Any<Embed>(), Arg.Any<RequestOptions>(), Arg.Any<AllowedMentions>(),
            Arg.Any<MessageReference>(), Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(),
            Arg.Any<MessageFlags>(), Arg.Any<PollProperties>());
        await channel.DidNotReceiveWithAnyArgs().SendFileAsync(default(Stream)!, default(string)!);
    }

    [Fact]
    public async Task Gif_DeveEnviarArquivoComoAnexo()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Payload)
        });
        var attachmentBytes = CaptureAttachmentBytes(channel);
        var interaction = CreateInteraction(GuildInteractionType.Gif, "https://media1.tenor.com/m/abc123/figurinha.gif");

        await service.SendResponseAsync(channel, interaction);

        await channel.Received(1).SendFileAsync(
            Arg.Any<Stream>(), "interacao.gif", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Embed>(),
            Arg.Any<RequestOptions>(), Arg.Any<bool>(), Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(),
            Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(),
            Arg.Any<PollProperties>());
        attachmentBytes().Should().Equal(Payload);
        await channel.DidNotReceiveWithAnyArgs().SendMessageAsync(default!);
    }

    [Fact]
    public async Task Gif_FalhaNoDownload_DeveEnviarEmbedComImagem()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var interaction = CreateInteraction(GuildInteractionType.Gif, "https://media1.tenor.com/m/abc123/figurinha.gif");

        await service.SendResponseAsync(channel, interaction);

        await channel.Received(1).SendMessageAsync(
            Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Is<Embed>(e => e != null && e.Image.HasValue && e.Image.Value.Url == "https://media1.tenor.com/m/abc123/figurinha.gif"),
            Arg.Any<RequestOptions>(), Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(),
            Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(),
            Arg.Any<PollProperties>());
        await channel.DidNotReceiveWithAnyArgs().SendFileAsync(default(Stream)!, default(string)!);
    }

    [Fact]
    public async Task Audio_DeveEnviarArquivoComoAnexo()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Payload)
        });
        var attachmentBytes = CaptureAttachmentBytes(channel);
        var interaction = CreateInteraction(GuildInteractionType.Audio, MediaUrl);

        await service.SendResponseAsync(channel, interaction);

        await channel.Received(1).SendFileAsync(
            Arg.Any<Stream>(), "interacao.mp3", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Embed>(),
            Arg.Any<RequestOptions>(), Arg.Any<bool>(), Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(),
            Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(),
            Arg.Any<PollProperties>());
        attachmentBytes().Should().Equal(Payload);
        await channel.DidNotReceiveWithAnyArgs().SendMessageAsync(default!);
    }

    [Fact]
    public async Task Video_DeveEnviarArquivoComoAnexo()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Payload)
        });
        var attachmentBytes = CaptureAttachmentBytes(channel);
        var interaction = CreateInteraction(GuildInteractionType.Video, "https://cdn.example.com/clipe.mp4");

        await service.SendResponseAsync(channel, interaction);

        await channel.Received(1).SendFileAsync(
            Arg.Any<Stream>(), "interacao.mp4", Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Embed>(),
            Arg.Any<RequestOptions>(), Arg.Any<bool>(), Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(),
            Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(),
            Arg.Any<PollProperties>());
        attachmentBytes().Should().Equal(Payload);
        await channel.DidNotReceiveWithAnyArgs().SendMessageAsync(default!);
    }

    [Fact]
    public async Task Audio_RespostaComErroHttp_DeveEnviarApenasLink()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var interaction = CreateInteraction(GuildInteractionType.Audio, MediaUrl);

        await service.SendResponseAsync(channel, interaction);

        await AssertFallbackLinkAsync(channel, interaction);
    }

    [Fact]
    public async Task Video_FalhaDeRede_DeveEnviarApenasLink()
    {
        var (service, channel) = CreateService(_ => throw new HttpRequestException("boom"));
        var interaction = CreateInteraction(GuildInteractionType.Video, "https://cdn.example.com/clipe.mp4");

        await service.SendResponseAsync(channel, interaction);

        await AssertFallbackLinkAsync(channel, interaction);
    }

    [Fact]
    public async Task Audio_MidiaAcimaDoLimite_DeveEnviarApenasLink()
    {
        var (service, channel) = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new OversizeContent()
        });
        var interaction = CreateInteraction(GuildInteractionType.Audio, MediaUrl);

        await service.SendResponseAsync(channel, interaction);

        await AssertFallbackLinkAsync(channel, interaction);
    }

    private static async Task AssertFallbackLinkAsync(IMessageChannel channel, GuildInteraction interaction)
    {
        await channel.Received(1).SendMessageAsync(
            interaction.Response, Arg.Any<bool>(), Arg.Any<Embed>(), Arg.Any<RequestOptions>(),
            Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(), Arg.Any<MessageComponent>(),
            Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(), Arg.Any<PollProperties>());
        await channel.DidNotReceiveWithAnyArgs().SendFileAsync(default(Stream)!, default(string)!);
    }

    private static Func<byte[]?> CaptureAttachmentBytes(IMessageChannel channel)
    {
        byte[]? captured = null;
        channel
            .When(c => c.SendFileAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<Embed>(),
                Arg.Any<RequestOptions>(), Arg.Any<bool>(), Arg.Any<AllowedMentions>(), Arg.Any<MessageReference>(),
                Arg.Any<MessageComponent>(), Arg.Any<ISticker[]>(), Arg.Any<Embed[]>(), Arg.Any<MessageFlags>(),
                Arg.Any<PollProperties>()))
            .Do(call =>
            {
                using var buffer = new MemoryStream();
                call.Arg<Stream>().CopyTo(buffer);
                captured = buffer.ToArray();
            });

        return () => captured;
    }

    private static (ChatInteractionService Service, IMessageChannel Channel) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(new StubHttpMessageHandler(responder)));

        var service = new ChatInteractionService(
            Substitute.For<IGuildInteractionRepository>(),
            factory,
            NullLogger<ChatInteractionService>.Instance);

        return (service, Substitute.For<IMessageChannel>());
    }

    private static GuildInteraction CreateInteraction(GuildInteractionType tipo, string response)
        => new()
        {
            GuildId = 1,
            Trigger = "teste",
            Tipo = tipo,
            Response = response
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private sealed class OversizeContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = ChatInteractionService.MaxAttachmentBytes + 1;
            return true;
        }
    }
}
