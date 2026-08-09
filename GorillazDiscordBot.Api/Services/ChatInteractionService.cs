using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public interface IChatInteractionService
{
    Task<bool> TryRespondAsync(SocketCommandContext context, string prefix);
}

public class ChatInteractionService : IChatInteractionService
{
    private readonly IGuildInteractionRepository _interactionRepository;
    private readonly ILogger<ChatInteractionService> _logger;

    public ChatInteractionService(
        IGuildInteractionRepository interactionRepository,
        ILogger<ChatInteractionService> logger)
    {
        _interactionRepository = interactionRepository;
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

        await context.Channel.SendMessageAsync(interaction.Response);
        _logger.LogInformation(
            "Interação '{trigger}' executada no servidor {guild} por {user}",
            trigger, context.Guild.Name, context.User.Username);

        return true;
    }
}
