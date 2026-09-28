using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("anti-raid", "Proteção automática contra picos de entrada (raid)")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class AntiRaidSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public AntiRaidSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuração atual do anti-raid")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();

        var embed = new EmbedBuilder()
            .WithTitle("🚨 Anti-raid")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .AddField("Limite", $"{settings.MaxJoinsPerMinute} entradas / {settings.WindowSeconds}s", true)
            .WithStandardFooter("Use /anti-raid para configurar")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("ativar", "Ativa ou desativa o anti-raid")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Enabled = ativo;
        await SaveAsync(settings);

        await RespondAsync(ativo
            ? "🚨 Anti-raid **ativado** neste servidor."
            : "🚨 Anti-raid **desativado** neste servidor.");
    }

    [SlashCommand("limite", "Define o limite de entradas que dispara o anti-raid")]
    public async Task LimiteAsync(
        [Summary("entradas", "Quantas entradas na janela disparam o ban automático")] int entradas,
        [Summary("segundos", "Janela de tempo (segundos)")] int segundos = 60)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.MaxJoinsPerMinute = Math.Clamp(entradas, 2, 50);
        settings.WindowSeconds = Math.Clamp(segundos, 5, 3600);
        await SaveAsync(settings);

        await RespondAsync($"🚨 Anti-raid: **{settings.MaxJoinsPerMinute} entradas** por **{settings.WindowSeconds}s**.");
    }

    private async Task<RaidSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Raid;
    }

    private async Task SaveAsync(RaidSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Raid = settings;
        await _guildRepository.SaveAsync(guild);
    }
}