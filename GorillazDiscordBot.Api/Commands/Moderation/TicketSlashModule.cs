using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Api.Commands.Moderation;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("ticket", "Configuração do sistema de tickets de suporte")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class TicketSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public TicketSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuração atual do sistema de tickets")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();

        var embed = new EmbedBuilder()
            .WithTitle("🎫 Sistema de tickets")
            .WithStandardFooter("Use /ticket para configurar")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("categoria", "Define a categoria onde os tickets serão criados")]
    public async Task CategoriaAsync(
        [Summary("categoria", "Categoria de canais do Discord")] ICategoryChannel categoria)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.CategoryId = categoria.Id;
        await SaveAsync(settings);

        await RespondAsync($"🎫 Tickets serão criados na categoria {categoria.Name}.");
    }

    [SlashCommand("equipe", "Define o cargo da equipe de suporte")]
    public async Task EquipeAsync(
        [Summary("cargo", "Cargo com acesso aos tickets")] IRole cargo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.SupportRoleId = cargo.Id;
        await SaveAsync(settings);

        await RespondAsync($"🎫 A equipe de suporte é o cargo {cargo.Mention}.");
    }

    [SlashCommand("ativar", "Ativa ou desativa o sistema de tickets")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Enabled = ativo;
        await SaveAsync(settings);

        await RespondAsync(ativo
            ? "🎫 Sistema de tickets **ativado**."
            : "🎫 Sistema de tickets **desativado**.");
    }

    private async Task<TicketSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Ticket;
    }

    private async Task SaveAsync(TicketSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Ticket = settings;
        await _guildRepository.SaveAsync(guild);
    }
}