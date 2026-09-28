using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public sealed record CommandPolicyResult(bool Allowed, string? Reason)
{
    public static CommandPolicyResult Ok { get; } = new(true, null);

    public static CommandPolicyResult Blocked(string reason) => new(false, reason);
}

public class CommandPolicyService
{
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly ILogger<CommandPolicyService> _logger;
    private readonly ConcurrentDictionary<(ulong GuildId, ulong UserId, string Command), DateTime> _lastUse = new();

    public CommandPolicyService(ISettingsRepository<Guild> guildRepository, ILogger<CommandPolicyService> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    public async Task<CommandPolicyResult> CheckAsync(SocketGuild guild, SocketGuildUser user, string commandKey)
    {
        try
        {
            var settings = await _guildRepository.GetAsync(guild.Id);
            if (settings == null)
                return CommandPolicyResult.Ok;

            if (IsExempt(user, guild))
                return CommandPolicyResult.Ok;

            var permission = settings.Permission ?? new PermissionSettings();
            if (permission.Enabled)
            {
                var match = permission.Entries.FirstOrDefault(e =>
                    string.Equals(e.Command, commandKey, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    var hasRole = user.Roles.Any(r => r.Id == match.RoleId);
                    if (!match.Allowed || !hasRole)
                    {
                        return CommandPolicyResult.Blocked(
                            $"🔒 Seu cargo não tem permissão para usar `{commandKey}` neste servidor.");
                    }
                }
            }

            var cooldown = settings.Cooldown ?? new CooldownSettings();
            if (cooldown.Enabled)
            {
                var seconds = cooldown.CommandSeconds.TryGetValue(commandKey, out var overrideSeconds)
                    ? overrideSeconds
                    : cooldown.DefaultSeconds;

                if (seconds > 0)
                {
                    var now = DateTime.UtcNow;
                    var key = (guild.Id, user.Id, commandKey);
                    var last = _lastUse.GetOrAdd(key, now);

                    if (last != now)
                    {
                        var elapsed = (now - last).TotalSeconds;
                        if (elapsed < seconds)
                        {
                            return CommandPolicyResult.Blocked(
                                $"⏳ Calma! Espere **{Math.Ceiling(seconds - elapsed)}s** antes de usar `{commandKey}` novamente.");
                        }
                    }

                    _lastUse[key] = now;
                }
            }

            return CommandPolicyResult.Ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao verificar política de comando {commandKey}", commandKey);
            return CommandPolicyResult.Ok;
        }
    }

    private static bool IsExempt(SocketGuildUser user, SocketGuild guild)
        => user.Id == guild.OwnerId
           || user.GuildPermissions.Administrator
           || user.GuildPermissions.ManageGuild;
}