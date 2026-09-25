using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("prefixo", "Configuracao do prefixo de comandos de texto")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class PrefixSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const int MaxPrefixLength = 10;

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<BotOptions> _botOptions;

    public PrefixSlashModule(
        ISettingsRepository<Guild> guildRepository,
        IOptions<BotOptions> botOptions)
    {
        _guildRepository = guildRepository;
        _botOptions = botOptions;
    }

    [SlashCommand("status", "Mostra o prefixo atual")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var prefix = await GetCurrentPrefixAsync();
        await RespondAsync($"Prefixo atual: `{prefix}`");
    }

    [SlashCommand("definir", "Define o prefixo de comandos de texto")]
    public async Task DefinirAsync(
        [Summary("prefixo", "Novo prefixo")] string prefixo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var trimmed = prefixo.Trim();
        if (trimmed.Length == 0)
        {
            await RespondAsync("Informe um prefixo valido.");
            return;
        }

        if (trimmed.Length > MaxPrefixLength)
        {
            await RespondAsync("Prefixo muito longo.");
            return;
        }

        var settings = await GetSettingsAsync();
        settings.Prefix = trimmed;
        await SaveAsync(settings);

        await RespondAsync($"Prefixo definido: `{trimmed}`");
    }

[SlashCommand("remover", "Volta ao prefixo padrao")]
    public async Task RemoverAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Prefix = null;
        await SaveAsync(settings);

        var defaultPrefix = _botOptions.Value.CommandPrefix;
        await RespondAsync($"Prefixo resetado para `{defaultPrefix}`");
    }

    private async Task<string> GetCurrentPrefixAsync()
    {
        var settings = await GetSettingsAsync();
        return !string.IsNullOrWhiteSpace(settings.Prefix) ? settings.Prefix : _botOptions.Value.CommandPrefix;
    }

    private async Task<PrefixSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Prefix;
    }

    private async Task SaveAsync(PrefixSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Prefix = settings;
        await _guildRepository.SaveAsync(guild);
    }
}