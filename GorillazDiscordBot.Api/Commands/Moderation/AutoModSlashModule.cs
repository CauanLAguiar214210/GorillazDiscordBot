using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("automod", "Auto-moderação do servidor (palavras bloqueadas e proteção contra flood)")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class AutoModSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public AutoModSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuração atual da auto-moderação")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();

        var embed = new EmbedBuilder()
            .WithTitle("🛡️ Auto-moderação")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .AddField("Ação", settings.Action == AutomodAction.Timeout ? "Excluir + timeout" : "Excluir mensagem", true)
            .AddField("Limite de flood", $"{settings.MaxMessagesPerInterval} msgs / {settings.IntervalSeconds}s", true)
            .AddField("Timeout", $"{settings.TimeoutMinutes} min", true)
            .AddField("Convites", SettingsBoolean(settings.BlockInvites), true)
            .AddField("@everyone/@here", SettingsBoolean(settings.BlockEveryonePings), true)
            .AddField("Menções por msg", settings.MaxMentionsPerMessage > 0 ? $"máx. {settings.MaxMentionsPerMessage}" : BotConstants.NotSet, true)
            .AddField("Strikes", settings.EnableStrikes
                ? $"timeout em {settings.StrikeTimeoutWarnings}, ban em {settings.StrikeBanWarnings}"
                : BotConstants.NotSet, true)
            .WithDescription(settings.BlockedWords.Count == 0
                ? "Nenhuma palavra bloqueada configurada."
                : $"**Palavras bloqueadas ({settings.BlockedWords.Count}):**\n{string.Join(", ", settings.BlockedWords.Select(FormatWord))}")
            .WithStandardFooter("Use /automod para configurar")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("ativar", "Ativa ou desativa a auto-moderação")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Enabled = ativo;
        await SaveAsync(settings);

        await RespondAsync(ativo
            ? "🛡️ Auto-moderação **ativada** neste servidor."
            : "🛡️ Auto-moderação **desativada** neste servidor.");
    }

    [SlashCommand("acao", "Define a ação aplicada ao infrator (excluir ou timeout)")]
    public async Task AcaoAsync(
        [Summary("acao", "Ação a aplicar: excluir ou timeout")] AutomodAction acao)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Action = acao;
        await SaveAsync(settings);

        await RespondAsync($"🛡️ Ação da auto-moderação definida para **{(acao == AutomodAction.Timeout ? "excluir + timeout" : "excluir mensagem")}**.");
    }

    [SlashCommand("limite", "Define o limite de flood (mensagens por intervalo)")]
    public async Task LimiteAsync(
        [Summary("mensagens", "Quantas mensagens em {segundos}s disparam a ação")] int mensagens,
        [Summary("segundos", "Janela de tempo (segundos)")] int segundos = 10)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.MaxMessagesPerInterval = Math.Clamp(mensagens, 2, 100);
        settings.IntervalSeconds = Math.Clamp(segundos, 1, 3600);
        await SaveAsync(settings);

        await RespondAsync($"🛡️ Limite de flood: **{settings.MaxMessagesPerInterval} mensagens** por **{settings.IntervalSeconds}s**.");
    }

    [SlashCommand("timeout", "Define a duração do timeout em minutos (ação: timeout)")]
    public async Task TimeoutAsync(
        [Summary("minutos", "Duração do timeout (mín. 1)")] int minutos)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.TimeoutMinutes = Math.Clamp(minutos, 1, 10080);
        await SaveAsync(settings);

        await RespondAsync($"🛡️ Timeout da auto-moderação: **{settings.TimeoutMinutes} minutos**.");
    }

    [SlashCommand("palavra-adicionar", "Adiciona uma palavra bloqueada")]
    public async Task PalavraAdicionarAsync(
        [Summary("palavra", "Palavra (ou parte dela) a bloquear")] string palavra)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var cleaned = palavra.Trim().ToLowerInvariant();
        if (cleaned.Length == 0)
        {
            await RespondAsync("❌ Informe uma palavra válida.");
            return;
        }

        var settings = await GetSettingsAsync();
        if (settings.BlockedWords.Contains(cleaned))
        {
            await RespondAsync($"⚠️ A palavra `{cleaned}` já está na lista.");
            return;
        }

        settings.BlockedWords.Add(cleaned);
        await SaveAsync(settings);

        await RespondAsync($"🛡️ Palavra `{cleaned}` adicionada à lista (total: {settings.BlockedWords.Count}).");
    }

    [SlashCommand("palavra-remover", "Remove uma palavra bloqueada")]
    public async Task PalavraRemoverAsync(
        [Summary("palavra", "Palavra a remover da lista")] string palavra)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var cleaned = palavra.Trim().ToLowerInvariant();
        var settings = await GetSettingsAsync();

        if (settings.BlockedWords.Remove(cleaned))
        {
            await SaveAsync(settings);
            await RespondAsync($"🛡️ Palavra `{cleaned}` removida (total: {settings.BlockedWords.Count}).");
            return;
        }

        await RespondAsync($"❌ A palavra `{cleaned}` não está na lista.");
    }

    [SlashCommand("palavras", "Lista as palavras bloqueadas")]
    public async Task PalavrasAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();

        if (settings.BlockedWords.Count == 0)
        {
            await RespondAsync("📭 Nenhuma palavra bloqueada neste servidor.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"🛡️ Palavras bloqueadas ({settings.BlockedWords.Count})")
            .WithGoldTheme()
            .WithDescription(string.Join("\n", settings.BlockedWords.Select((word, i) => $"{i + 1}. `{word}`")))
            .WithStandardFooter("Use /automod palavra-remover para remover")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("proteger", "Proteções de convites, @everyone/@here e limite de menções")]
    public async Task ProtegerAsync(
        [Summary("convites", "Bloqueia convites do Discord")] bool? convites = null,
        [Summary("marcacoes-todos", "Bloqueia @everyone e @here")] bool? marcacoesTodos = null,
        [Summary("limite-mencoes", "Limite de menções por mensagem (0 desativa)")] int? limiteMencoes = null)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        if (convites.HasValue) settings.BlockInvites = convites.Value;
        if (marcacoesTodos.HasValue) settings.BlockEveryonePings = marcacoesTodos.Value;
        if (limiteMencoes.HasValue) settings.MaxMentionsPerMessage = Math.Clamp(limiteMencoes.Value, 0, 50);
        await SaveAsync(settings);

        await RespondAsync(
            $"- 🚫 **Convites:** {SettingsBoolean(settings.BlockInvites)}\n" +
            $"- 📣 **@everyone/@here:** {SettingsBoolean(settings.BlockEveryonePings)}\n" +
            $"- 🔔 **Menções por mensagem:** {settings.MaxMentionsPerMessage}");
    }

    [SlashCommand("strikes", "Ativa/desativa os strikes e define os limites de avisos para punição")]
    public async Task StrikesAsync(
        [Summary("ativar", "true ativa, false desativa os strikes")] bool? ativar = null,
        [Summary("timeout", "Aplicar timeout a partir de N avisos")] int? timeout = null,
        [Summary("banir", "Banir a partir de N avisos")] int? banir = null)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        if (ativar.HasValue) settings.EnableStrikes = ativar.Value;
        if (timeout.HasValue) settings.StrikeTimeoutWarnings = Math.Clamp(timeout.Value, 1, 100);
        if (banir.HasValue) settings.StrikeBanWarnings = Math.Clamp(banir.Value, 1, 100);
        await SaveAsync(settings);

        await RespondAsync(
            $"⚡ Strikes **{(settings.EnableStrikes ? "ativados" : "desativados")}** neste servidor.\n" +
            $"- 🤫 Timeout com **{settings.StrikeTimeoutWarnings}+ avisos**\n" +
            $"- ⛔ Banimento com **{settings.StrikeBanWarnings}+ avisos**");
    }

    private static string SettingsBoolean(bool value) => value ? "🟢 Ativo" : "🔴 Desativado";

    private async Task<AutomodSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Automod ?? new AutomodSettings();
    }

    private async Task SaveAsync(AutomodSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Automod = settings;
        await _guildRepository.SaveAsync(guild);
    }

    private static string FormatWord(string word)
        => $"`{word}`";
}