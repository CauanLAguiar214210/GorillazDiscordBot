using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public interface IChatInteractionService
{
    Task<bool> TryRespondAsync(SocketCommandContext context, string prefix);
}

public class ChatInteractionService : IChatInteractionService
{
    internal const string MediaHttpClientName = "interaction-media";
    internal const int MaxAttachmentBytes = 10 * 1024 * 1024;

    private const int CopyBufferSize = 81_920;

    private static readonly string[] AudioExtensions = [".mp3", ".ogg", ".wav", ".m4a"];
    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".mov", ".avi", ".mkv"];
    private static readonly string[] ImageExtensions = [".gif", ".png", ".jpg", ".jpeg", ".webp"];

    private readonly IGuildInteractionRepository _interactionRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatInteractionService> _logger;

    public ChatInteractionService(
        IGuildInteractionRepository interactionRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatInteractionService> logger)
    {
        _interactionRepository = interactionRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> TryRespondAsync(SocketCommandContext context, string prefix)
    {
        if (context.Guild == null) return false;

        var content = context.Message.Content;
        if (string.IsNullOrWhiteSpace(content)) return false;

        var afterPrefix = content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? content[prefix.Length..].Trim()
            : content.Trim();

        if (string.IsNullOrEmpty(afterPrefix)) return false;

        var trigger = afterPrefix.Split(' ', 2)[0].ToLowerInvariant();

        var interaction = await _interactionRepository.GetAsync(context.Guild.Id, trigger);
        if (interaction == null) return false;

        await SendResponseAsync(context.Channel, interaction);
        _logger.LogInformation(
            "Interação '{trigger}' executada no servidor {guild} por {user}",
            trigger, context.Guild.Name, context.User.GetDisplayName());

        return true;
    }

    internal async Task SendResponseAsync(IMessageChannel channel, GuildInteraction interaction)
    {
        switch (interaction.Tipo)
        {
            case GuildInteractionType.Gif:
                if (!await TrySendAttachmentAsync(channel, interaction))
                    await channel.SendMessageAsync(embed: new EmbedBuilder().WithImageUrl(interaction.Response).Build());
                return;

            case GuildInteractionType.Audio:
            case GuildInteractionType.Video:
                if (!await TrySendAttachmentAsync(channel, interaction))
                    await channel.SendMessageAsync(interaction.Response);
                return;

            default:
                await channel.SendMessageAsync(interaction.Response);
                return;
        }
    }

    private async Task<bool> TrySendAttachmentAsync(IMessageChannel channel, GuildInteraction interaction)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(MediaHttpClientName);
            using var response = await client.GetAsync(interaction.Response, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxAttachmentBytes)
            {
                _logger.LogWarning(
                    "Mídia da interação '{trigger}' excede {limit} MB — enviando apenas o link",
                    interaction.Trigger, MaxAttachmentBytes / (1024 * 1024));
                return false;
            }

            using var media = new MemoryStream();
            await using (var source = await response.Content.ReadAsStreamAsync())
            {
                await CopyMediaAsync(source, media);
            }

            media.Position = 0;
            await channel.SendFileAsync(media, AttachmentFileName(interaction));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao enviar mídia da interação '{trigger}' como anexo — enviando apenas o link",
                interaction.Trigger);
            return false;
        }
    }

    private static async Task CopyMediaAsync(Stream source, Stream destination)
    {
        var buffer = new byte[CopyBufferSize];
        long totalBytes = 0;

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer.AsMemory())) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaxAttachmentBytes)
                throw new InvalidOperationException(
                    $"Mídia maior que o limite de {MaxAttachmentBytes / (1024 * 1024)} MB para anexo.");

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
        }
    }

    private static string AttachmentFileName(GuildInteraction interaction)
    {
        var supported = interaction.Tipo switch
        {
            GuildInteractionType.Audio => AudioExtensions,
            GuildInteractionType.Gif => ImageExtensions,
            _ => VideoExtensions
        };

        if (Uri.TryCreate(interaction.Response, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.ToLowerInvariant();
            var extension = supported.FirstOrDefault(ext => path.EndsWith(ext));
            if (extension != null)
                return $"interacao{extension}";
        }

        return $"interacao{supported[0]}";
    }
}
