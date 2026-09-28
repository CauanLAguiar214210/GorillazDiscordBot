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
        var entries = settings.Entries
            .OrderBy(e => e.Command)
            .Select(e => $"- `{e.Command}` → <@&{e.RoleId}>");

        var embed = new EmbedBuilder()
            .WithTitle("🔒 Permissões por comando")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .WithDescription(settings.Entries.Count == 0
                ? "Nenhuma permissão configurada."
                : $"**Regras ({settings.Entries.Count}):**\n{string.Join("\n", entries)}")
            .WithStandardFooter("Use /permissao conceder <comando> <cargo>")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("ativar", "Ativa ou desativa as permissões por comando")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Enabled = ativo;
        await SaveAsync(settings);

        await RespondAsync(ativo
            ? "🔒 Permissões por comando **ativadas** neste servidor."
            : "🔒 Permissões por comando **desativadas** neste servidor.");
    }

    [SlashCommand("conceder", "Permite um cargo usar um comando (passa a ser restrito a cargos autorizados)")]
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

        await RespondAsync($"🔒 `{comando}` disponível para {cargo.Mention}.");
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

        await RespondAsync($"🔒 `{comando}` indisponível para {cargo.Mention}.");
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