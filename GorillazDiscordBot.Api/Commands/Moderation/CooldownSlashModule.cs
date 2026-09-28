using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("cooldown", "Configuracao de intervalos entre usos de comandos")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class CooldownSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public CooldownSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuracao atual de cooldowns")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Cooldowns")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("definir", "Define o intervalo entre usos de um comando")]
    public async Task DefinirAsync(
        [Summary("comando", "Nome do comando")] string comando,
        [Summary("segundos", "Intervalo em segundos")] int segundos)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.CommandSeconds[comando] = segundos;
        await SaveAsync(settings);

        await RespondAsync($"Cooldown de /{comando} definido para {segundos}s.");
    }

    [SlashCommand("remover", "Remove o cooldown de um comando")]
    public async Task RemoverAsync(
        [Summary("comando", "Nome do comando")] string comando)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.CommandSeconds.Remove(comando);
        await SaveAsync(settings);

        await RespondAsync($"Cooldown de /{comando} removido.");
    }

    private async Task<CooldownSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Cooldown;
    }

    private async Task SaveAsync(CooldownSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Cooldown = settings;
        await _guildRepository.SaveAsync(guild);
    }
}