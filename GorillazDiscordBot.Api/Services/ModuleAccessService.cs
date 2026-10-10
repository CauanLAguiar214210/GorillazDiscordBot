using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public sealed record ModuleAccessResult(bool Allowed, string ModuleId, string? Reason)
{
    public static ModuleAccessResult Allow(string moduleId) => new(true, moduleId, null);
}

public sealed record ModuleStatus(string Id, string Name, bool Enabled);

public sealed class ModuleAccessService
{
    public const string Core = "core";

    private static readonly IReadOnlyDictionary<string, (string Id, string Name)> CommandModules =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["carteira"] = ("economy", "Economia"),
            ["banco"] = ("economy", "Economia"),
            ["crime"] = ("economy", "Economia"),
            ["daily"] = ("economy", "Economia"),
            ["trabalho"] = ("jobs", "Trabalho"),
            ["roubar"] = ("economy", "Economia"),
            ["ranking"] = ("economy", "Economia"),
            ["loja"] = ("shop", "Loja"),
            ["comprar"] = ("shop", "Loja"),
            ["usar"] = ("shop", "Loja"),
            ["inventario"] = ("shop", "Loja"),
            ["vender"] = ("shop", "Loja"),
            ["equipar"] = ("shop", "Loja"),
            ["desequipar"] = ("shop", "Loja"),
            ["cassino"] = ("casino", "Cassino"),
            ["veiculo"] = ("vehicles", "Veículos"),
            ["garagem"] = ("vehicles", "Veículos"),
            ["audio"] = ("audio", "Áudio"),
            ["tocar"] = ("audio", "Áudio"),
            ["teste"] = ("audio", "Áudio"),
            ["parar"] = ("audio", "Áudio"),
            ["sons"] = ("audio", "Áudio"),
            ["automod"] = ("automod", "AutoMod"),
            ["anti-raid"] = ("anti-raid", "Anti-raid"),
            ["modlog"] = ("audit-log", "Logs de auditoria"),
            ["interacao"] = ("interactions", "Interações"),
            ["perfil"] = ("profile", "Perfil"),
            ["ensino"] = ("education", "Ensino"),
            ["contas"] = ("linked-accounts", "Contas vinculadas"),
            ["limpar"] = ("moderation", "Moderação"),
            ["limpar-tudo"] = ("moderation", "Moderação"),
            ["expulsar"] = ("moderation", "Moderação"),
            ["banir"] = ("moderation", "Moderação"),
            ["desbanir"] = ("moderation", "Moderação"),
            ["banimentos"] = ("moderation", "Moderação"),
            ["timeout"] = ("moderation", "Moderação"),
            ["avisar"] = ("moderation", "Moderação"),
            ["avisos"] = ("moderation", "Moderação"),
            ["removeaviso"] = ("moderation", "Moderação"),
            ["ritmolento"] = ("moderation", "Moderação"),
            ["trancar"] = ("moderation", "Moderação"),
            ["destrancar"] = ("moderation", "Moderação"),
            ["moderacao"] = ("moderation", "Moderação")
        };

    private static readonly IReadOnlyList<(string Id, string Name)> Modules =
        CommandModules.Values.Distinct().OrderBy(x => x.Name).ToArray();

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly ILogger<ModuleAccessService> _logger;

    public ModuleAccessService(
        ISettingsRepository<Guild> guildRepository,
        ILogger<ModuleAccessService> logger)
    {
        _guildRepository = guildRepository;
        _logger = logger;
    }

    public string ResolveModule(string commandName)
        => CommandModules.TryGetValue(commandName, out var module) ? module.Id : Core;

    public async Task<ModuleAccessResult> CheckAsync(ulong guildId, string commandName)
    {
        var moduleId = ResolveModule(commandName);
        if (moduleId == Core)
            return ModuleAccessResult.Allow(moduleId);

        try
        {
            var guild = await _guildRepository.GetAsync(guildId);
            if (IsEnabled(guild, moduleId))
                return ModuleAccessResult.Allow(moduleId);

            var name = CommandModules[commandName].Name;
            return new(false, moduleId, $"O módulo **{name}** não está disponível neste servidor.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao consultar acesso ao módulo {moduleId} na guilda {guildId}", moduleId, guildId);
            return ModuleAccessResult.Allow(moduleId);
        }
    }

    public async Task<IReadOnlyList<ModuleStatus>> GetStatusesAsync(ulong guildId)
    {
        var guild = await _guildRepository.GetAsync(guildId);
        return Modules.Select(module => new ModuleStatus(
            module.Id,
            module.Name,
            IsEnabled(guild, module.Id))).ToArray();
    }

    private static bool IsEnabled(Guild guild, string moduleId)
        => guild.Modules?.Enabled is null
           || !guild.Modules.Enabled.TryGetValue(moduleId, out var enabled)
           || enabled;
}
