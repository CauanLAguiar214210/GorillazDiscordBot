using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("modlog", "Configuração do log de auditoria do servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class ModLogSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public ModLogSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuração atual do log de auditoria")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();

        var embed = new EmbedBuilder()
            .WithTitle("📋 Log de auditoria")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .AddField("Canal", settings.ChannelId.HasValue
                ? $"<#{settings.ChannelId.Value}>"
                : BotConstants.NotSet, true)
            .AddField("Mensagens apagadas", FormatBoolean(settings.LogMessageDeletes), true)
            .AddField("Mensagens editadas", FormatBoolean(settings.LogMessageEdits), true)
            .AddField("Banimentos", FormatBoolean(settings.LogBans), true)
            .AddField("Entradas/saídas", FormatBoolean(settings.LogMembers), true)
            .WithStandardFooter("Use /modlog para configurar")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("canal", "Define o canal onde os logs serão enviados")]
    public async Task CanalAsync(
        [Summary("canal", "Canal de texto para os logs")] ITextChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.ChannelId = canal.Id;
        await SaveAsync(settings);

        await RespondAsync($"📋 Log de auditoria direcionado para {canal.Mention}.");
    }

    [SlashCommand("ativar", "Ativa ou desativa o log de auditoria")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Enabled = ativo;
        await SaveAsync(settings);

        await RespondAsync(ativo
            ? "📋 Log de auditoria **ativado**."
            : "📋 Log de auditoria **desativado**.");
    }

    [SlashCommand("eventos", "Ativa ou desativa eventos específicos do log")]
    public async Task EventosAsync(
        [Summary("mensagens-apagadas", "Log de mensagens apagadas")] bool? mensagensApagadas = null,
        [Summary("mensagens-editadas", "Log de mensagens editadas")] bool? mensagensEditadas = null,
        [Summary("banimentos", "Log de banimentos/desbanimentos")] bool? banimentos = null,
        [Summary("membros", "Log de entrada/saída de membros")] bool? membros = null)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        if (mensagensApagadas.HasValue) settings.LogMessageDeletes = mensagensApagadas.Value;
        if (mensagensEditadas.HasValue) settings.LogMessageEdits = mensagensEditadas.Value;
        if (banimentos.HasValue) settings.LogBans = banimentos.Value;
        if (membros.HasValue) settings.LogMembers = membros.Value;
        await SaveAsync(settings);

        await RespondAsync(
            $"- 🗑️ **Mensagens apagadas:** {FormatBoolean(settings.LogMessageDeletes)}\n" +
            $"- ✏️ **Mensagens editadas:** {FormatBoolean(settings.LogMessageEdits)}\n" +
            $"- 🚫 **Banimentos:** {FormatBoolean(settings.LogBans)}\n" +
            $"- 👥 **Entradas/saídas:** {FormatBoolean(settings.LogMembers)}");
    }

    private static string FormatBoolean(bool value) => value ? "🟢 Sim" : "🔴 Não";

    private async Task<GuildLogSettings> GetSettingsAsync()
        => (await _guildRepository.GetAsync(Context.Guild.Id)).Logs;

    private async Task SaveAsync(GuildLogSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Logs = settings;
        await _guildRepository.SaveAsync(guild);
    }
}