using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("sessao", "Configuracao de sessoes de mensagens deste servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class SessaoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public SessaoSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuracao atual dos paineis")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Sessoes")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("criar", "Cria um novo sessao")]
    public async Task CriarAsync(
        [Summary("nome", "Nome da sessao")] string nome,
        [Summary("canal", "Canal onde a sessao sera postada")] IChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Channels[nome] = canal.Id;
        await SaveAsync(settings);

        await RespondAsync($"Painel `/sessao sessao {nome}` criado no canal {MentionUtils.MentionChannel(canal.Id)}.");
    }

    [SlashCommand("remover", "Remove um sessao")]
    public async Task RemoverAsync(
        [Summary("nome", "Nome da sessao")] string nome)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Channels.Remove(nome);
        await SaveAsync(settings);

        await RespondAsync($"Painel `/sessao sessao {nome}` removido.");
    }

    private async Task<PanelSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Panel;
    }

    private async Task SaveAsync(PanelSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Panel = settings;
        await _guildRepository.SaveAsync(guild);
    }
}