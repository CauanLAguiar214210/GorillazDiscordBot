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

    private readonly ISettingsRepository<GuildVoiceSettings> _voiceRepository;
    private readonly ILogger<VoiceChannelService> _logger;
    private readonly ConcurrentDictionary<ulong, ulong> _managedChannels = new();

    public VoiceChannelService(ISettingsRepository<GuildVoiceSettings> voiceRepository, ILogger<VoiceChannelService> logger)
    {
        _voiceRepository = voiceRepository;
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

        var settings = await _voiceRepository.GetAsync(creatorChannel.Guild.Id);
        if (!settings.Enabled || settings.CreatorChannelId != creatorChannel.Id) return;

        if (await CreateAndMoveAsync(guildUser, creatorChannel))
        {
            _logger.LogInformation(
                "Canal de voz criado para {user} em {guild}",
                user.GetDisplayName(), creatorChannel.Guild.Name);
        }
    }

    private async Task<bool> CreateAndMoveAsync(IGuildUser user, SocketVoiceChannel creatorChannel)
    {
        var guild = creatorChannel.Guild;
        var name = BuildChannelName(user.GetDisplayName());
        name = ResolveUniqueName(guild, name);

        var channel = (IVoiceChannel)await guild.CreateVoiceChannelAsync(name, properties =>
        {
            properties.CategoryId = creatorChannel.CategoryId;
            properties.UserLimit = 10;
        });

        await channel.AddPermissionOverwriteAsync(guild.EveryoneRole,
            new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny));
        await channel.AddPermissionOverwriteAsync(user,
            new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow));
        await channel.AddPermissionOverwriteAsync(guild.CurrentUser,
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

    private static string BuildChannelName(string username)
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
        if (name.Length > MaxChannelNameLength) name = name[..MaxChannelNameLength];

        return name;
    }

    private static string ResolveUniqueName(SocketGuild guild, string name)
    {
        string prefixo = "Resenhando com ";
        string sufixo = "...";

        if (guild.VoiceChannels.All(channel => channel.Name != name))
            return prefixo + name + sufixo;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = prefixo + $"{name}-{i}" + sufixo;
            if (guild.VoiceChannels.All(channel => channel.Name != candidate))
                return candidate;
        }

        return name[..Math.Min(MaxChannelNameLength, name.Length - 9)];
    }
}
