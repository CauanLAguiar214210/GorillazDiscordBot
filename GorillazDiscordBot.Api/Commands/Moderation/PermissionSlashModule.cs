using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("permissao", "Configuracao de permissoes por comando")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class PermissionSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public PermissionSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra as permissoes de comandos")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Permissoes de comandos")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("conceder", "Permite um cargo usar um comando")]
    public async Task ConcederAsync(
        [Summary("comando", "Nome do comando")] string comando,
        [Summary("cargo", "Cargo autorizado")] IRole cargo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Entries.RemoveAll(e => e.Command == comando && e.RoleId == cargo.Id);
        settings.Entries.Add(new PermissionEntry { Command = comando, RoleId = cargo.Id, Allowed = true });
        await SaveAsync(settings);

        await RespondAsync($"/{comando} agora disponivel para {cargo.Mention}.");
    }

    [SlashCommand("revogar", "Remove a permissao de um cargo para um comando")]
    public async Task RevogarAsync(
        [Summary("comando", "Nome do comando")] string comando,
        [Summary("cargo", "Cargo")] IRole cargo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Entries.RemoveAll(e => e.Command == comando && e.RoleId == cargo.Id);
        await SaveAsync(settings);

        await RespondAsync($"/{comando} indisponivel para {cargo.Mention}.");
    }

    private async Task<PermissionSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Permission;
    }

    private async Task SaveAsync(PermissionSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Permission = settings;
        await _guildRepository.SaveAsync(guild);
    }
}