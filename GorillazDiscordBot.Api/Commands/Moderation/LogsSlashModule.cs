using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("logs", "Configuracao de registos de acoes deste servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class LogsSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public LogsSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuracao atual dos registos")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Paineis")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("criar", "Define o canal de registos")]
    public async Task CriarAsync(
        [Summary("nome", "Nome do registo")] string nome,
        [Summary("canal", "Canal onde os registos serao postados")] IChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.ChannelId = canal.Id;
        await SaveAsync(settings);

        await RespondAsync($"Painel `/painel sessao {nome}` criado no canal {MentionUtils.MentionChannel(canal.Id)}.");
    }

    [SlashCommand("remover", "Desativa os registos")]
    public async Task RemoverAsync(
        [Summary("nome", "Nome do registo")] string nome)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.ChannelId = null;
        await SaveAsync(settings);

        await RespondAsync($"Painel `/painel sessao {nome}` removido.");
    }

    private async Task<GuildLogSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Logs;
    }

    private async Task SaveAsync(GuildLogSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Logs = settings;
        await _guildRepository.SaveAsync(guild);
    }
}