using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("modlog", "Configuração do log de auditoria do servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class ModLogSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly GuildSettingsAccessor _settings;

    public ModLogSlashModule(GuildSettingsAccessor settings)
    {
        _settings = settings;
    }

    [SlashCommand("status", "Mostra a configuração atual do log de auditoria")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = (await _settings.GetAsync(Context.Guild.Id)).Logs;

        var embed = new EmbedBuilder()
            .WithTitle("📋 Log de auditoria")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .AddField("Canal", settings.ChannelId.HasValue
                ? $"<#{settings.ChannelId.Value}>"
                : BotConstants.NotSet, true)
            .AddField("Mensagens apagadas", FormatBoolean(settings.LogMessageDeletes), true)
            .AddField("Mensagens editadas", FormatBoolean(settings.LogMessageEdits), true)
            .AddField("Apagadas em massa", FormatBoolean(settings.LogBulkDeletes), true)
            .AddField("Banimentos", FormatBoolean(settings.LogBans), true)
            .AddField("Expulsões", FormatBoolean(settings.LogKicks), true)
            .AddField("Timeouts", FormatBoolean(settings.LogTimeouts), true)
            .AddField("Avisos", FormatBoolean(settings.LogWarnings), true)
            .AddField("Entradas/saídas", FormatBoolean(settings.LogMembers), true)
            .AddField("Apelidos", FormatBoolean(settings.LogNicknameChanges), true)
            .AddField("Cargos", FormatBoolean(settings.LogRoleChanges), true)
            .AddField("Voz", FormatBoolean(settings.LogVoice), true)
            .AddField("Canais", FormatBoolean(settings.LogChannelChanges), true)
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

        await _settings.SaveAsync(Context.Guild.Id, g => g.Logs.ChannelId = canal.Id);

        await RespondAsync($"📋 Log de auditoria direcionado para {canal.Mention}.");
    }

    [SlashCommand("ativar", "Ativa ou desativa o log de auditoria")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        await _settings.SaveAsync(Context.Guild.Id, g => g.Logs.Enabled = ativo);

        await RespondAsync(ativo
            ? "📋 Log de auditoria **ativado**."
            : "📋 Log de auditoria **desativado**.");
    }

    [SlashCommand("eventos", "Ativa ou desativa eventos específicos do log")]
    public async Task EventosAsync(
        [Summary("mensagens-apagadas", "Log de mensagens apagadas")] bool? mensagensApagadas = null,
        [Summary("mensagens-editadas", "Log de mensagens editadas")] bool? mensagensEditadas = null,
        [Summary("apagadas-massa", "Log de apagadas em massa")] bool? apagadasMassa = null,
        [Summary("banimentos", "Log de banimentos/desbanimentos")] bool? banimentos = null,
        [Summary("expulsoes", "Log de expulsões")] bool? expulsoes = null,
        [Summary("timeouts", "Log de timeouts")] bool? timeouts = null,
        [Summary("avisos", "Log de avisos")] bool? avisos = null,
        [Summary("membros", "Log de entrada/saída de membros")] bool? membros = null,
        [Summary("apelidos", "Log de alteração de apelido")] bool? apelidos = null,
        [Summary("cargos", "Log de alteração de cargos")] bool? cargos = null,
        [Summary("voz", "Log de entrada/saída de canais de voz")] bool? voz = null,
        [Summary("canais", "Log de criação/remoção/edição de canais")] bool? canais = null)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = (await _settings.GetAsync(Context.Guild.Id)).Logs;
        if (mensagensApagadas.HasValue) settings.LogMessageDeletes = mensagensApagadas.Value;
        if (mensagensEditadas.HasValue) settings.LogMessageEdits = mensagensEditadas.Value;
        if (apagadasMassa.HasValue) settings.LogBulkDeletes = apagadasMassa.Value;
        if (banimentos.HasValue) settings.LogBans = banimentos.Value;
        if (expulsoes.HasValue) settings.LogKicks = expulsoes.Value;
        if (timeouts.HasValue) settings.LogTimeouts = timeouts.Value;
        if (avisos.HasValue) settings.LogWarnings = avisos.Value;
        if (membros.HasValue) settings.LogMembers = membros.Value;
        if (apelidos.HasValue) settings.LogNicknameChanges = apelidos.Value;
        if (cargos.HasValue) settings.LogRoleChanges = cargos.Value;
        if (voz.HasValue) settings.LogVoice = voz.Value;
        if (canais.HasValue) settings.LogChannelChanges = canais.Value;
        await _settings.SaveAsync(Context.Guild.Id, _ => { });

        await RespondAsync(
            $"- 🗑️ **Mensagens apagadas:** {FormatBoolean(settings.LogMessageDeletes)}\n" +
            $"- ✏️ **Mensagens editadas:** {FormatBoolean(settings.LogMessageEdits)}\n" +
            $"- 🧹 **Apagadas em massa:** {FormatBoolean(settings.LogBulkDeletes)}\n" +
            $"- 🚫 **Banimentos:** {FormatBoolean(settings.LogBans)}\n" +
            $"- 👢 **Expulsões:** {FormatBoolean(settings.LogKicks)}\n" +
            $"- 🤫 **Timeouts:** {FormatBoolean(settings.LogTimeouts)}\n" +
            $"- 🔨 **Avisos:** {FormatBoolean(settings.LogWarnings)}\n" +
            $"- 👥 **Entradas/saídas:** {FormatBoolean(settings.LogMembers)}\n" +
            $"- 🏷️ **Apelidos:** {FormatBoolean(settings.LogNicknameChanges)}\n" +
            $"- 🎭 **Cargos:** {FormatBoolean(settings.LogRoleChanges)}\n" +
            $"- 🎙️ **Voz:** {FormatBoolean(settings.LogVoice)}\n" +
            $"- 📁 **Canais:** {FormatBoolean(settings.LogChannelChanges)}");
    }

    private static string FormatBoolean(bool value) => value ? "🟢 Sim" : "🔴 Não";
}