using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class VoiceChannelService : IVoiceChannelService
{
    private const int MaxChannelNameLength = 100;
    private static readonly TimeSpan DeleteDelay = TimeSpan.FromSeconds(2);

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly ILogger<VoiceChannelService> _logger;
    private readonly ConcurrentDictionary<ulong, ulong> _managedChannels = new();

    public VoiceChannelService(ISettingsRepository<Guild> guildRepository, ILogger<VoiceChannelService> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    public async Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        try
        {
            await TryCreateChannelAsync(user, after);
            await TryDeleteChannelAsync(before, after);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mudança de voz de {user}", user.GetDisplayName());
        }
    }

    private async Task TryCreateChannelAsync(SocketUser user, SocketVoiceState after)
    {
        if (after.VoiceChannel is not SocketVoiceChannel creatorChannel) return;
        if (user is not IGuildUser guildUser) return;

        var guild = await _guildRepository.GetAsync(creatorChannel.Guild.Id);
        var settings = guild.VoiceChannels.FirstOrDefault(v =>
            v.CreatorChannelId == creatorChannel.Id && v.Enabled);
        if (settings == null) return;

        if (await CreateAndMoveAsync(guildUser, creatorChannel, settings))
        {
            _logger.LogInformation(
                "Canal de voz criado para {user} em {guild}",
                user.GetDisplayName(), creatorChannel.Guild.Name);
        }
    }

    private async Task<bool> CreateAndMoveAsync(IGuildUser user, SocketVoiceChannel creatorChannel, VoiceChannelSettings settings)
    {
        var guild = creatorChannel.Guild;
        var template = settings.NameTemplate ?? VoiceChannelSettings.DefaultNameTemplate;
        var baseName = SanitizeUsername(user.GetDisplayName());
        var name = ResolveUniqueName(guild, template, baseName);

        var channel = (IVoiceChannel)await guild.CreateVoiceChannelAsync(name, properties =>
        {
            properties.CategoryId = settings.CategoryId ?? creatorChannel.CategoryId;
            properties.UserLimit = settings.UserLimit ?? VoiceChannelSettings.DefaultUserLimit;
        });

        await channel.AddPermissionOverwriteAsync(guild.EveryoneRole,
            new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny));
        await channel.AddPermissionOverwriteAsync(user,
            new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow));

        _managedChannels[channel.Id] = user.Id;

        try
        {
            await user.ModifyAsync(properties => properties.Channel = new Optional<IVoiceChannel>(channel));
            return true;
        }
        catch (Exception ex)
        {
            _managedChannels.TryRemove(channel.Id, out _);
            await SafeDeleteAsync(channel);
            _logger.LogWarning(ex, "Não foi possível mover {user} para {channel}", user.GetDisplayName(), channel.Name);
            return false;
        }
    }

    private async Task TryDeleteChannelAsync(SocketVoiceState before, SocketVoiceState after)
    {
        if (before.VoiceChannel is not SocketVoiceChannel managedChannel) return;
        if (after.VoiceChannel?.Id == managedChannel.Id) return;
        if (!_managedChannels.ContainsKey(managedChannel.Id)) return;

        await TryDeleteIfEmptyAsync(managedChannel);
    }

    private async Task TryDeleteIfEmptyAsync(SocketVoiceChannel channel)
    {
        try
        {
            if (channel.ConnectedUsers.Count > 0) return;

            await Task.Delay(DeleteDelay);

            if (channel.ConnectedUsers.Count > 0) return;
            if (!_managedChannels.TryRemove(channel.Id, out _)) return;

            await channel.DeleteAsync();
            _logger.LogInformation("Canal de voz vazio deletado: {channel}", channel.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao deletar canal de voz gerenciado {channel}", channel.Name);
        }
    }

    private async Task SafeDeleteAsync(IVoiceChannel channel)
    {
        try
        {
            await channel.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao deletar canal temporário {channel}", channel.Name);
        }
    }

    private static string SanitizeUsername(string username)
    {
        var builder = new StringBuilder();
        char? previous = null;

        foreach (var character in username.ToLowerInvariant())
        {
            var isValid = char.IsLetterOrDigit(character) || character is '-' or '_';
            var normalized = isValid ? character : '-';

            if (normalized == '-' && (builder.Length == 0 || previous == '-'))
                continue;

            builder.Append(normalized);
            previous = normalized;
        }

        var name = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(name)) name = "canal";
        if (name.Length < 2) name = $"canal-{name}";

        return name;
    }

    private static string BuildChannelName(string template, string baseName)
    {
        var name = template.Replace("{name}", baseName, StringComparison.Ordinal);
        if (name.Length > MaxChannelNameLength)
            name = name[..MaxChannelNameLength];

        return name;
    }

    private static string ResolveUniqueName(SocketGuild guild, string template, string baseName)
    {
        var name = BuildChannelName(template, baseName);
        if (guild.VoiceChannels.All(channel => channel.Name != name))
            return name;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = BuildChannelName(template, $"{baseName}-{i}");
            if (guild.VoiceChannels.All(channel => channel.Name != candidate))
                return candidate;
        }

        return name;
    }
}