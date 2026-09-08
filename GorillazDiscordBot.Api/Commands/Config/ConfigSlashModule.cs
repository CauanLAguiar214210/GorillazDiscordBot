using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Api.Commands.Config;

[Group("config", "Configurações do servidor (boas-vindas, despedidas, voz e prefixo)")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class ConfigSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<BotOptions> _botOptions;

    public ConfigSlashModule(
        ISettingsRepository<Guild> guildRepository,
        IOptions<BotOptions> botOptions)
    {
        _guildRepository = guildRepository;
        _botOptions = botOptions;
    }

    [SlashCommand("status", "Mostra a configuração atual do servidor")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var prefix = GetCurrentPrefix(guild);

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração do Servidor")
            .WithGoldTheme()
            .AddField("Prefixo", $"`{prefix}`", true)
            .WithStatus("Boas-vindas", guild.Welcome.WelcomeEnabled)
            .WithChannelField("Canal de boas-vindas", guild.Welcome.WelcomeChannelId, Context.Guild)
            .AddField("Mensagem de boas-vindas", guild.Welcome.WelcomeMessage, false)
            .WithStatus("Despedidas", guild.Welcome.GoodbyeEnabled)
            .WithChannelField("Canal de despedidas", guild.Welcome.GoodbyeChannelId, Context.Guild)
            .AddField("Mensagem de despedidas", guild.Welcome.GoodbyeMessage, false);

        embed = guild.VoiceChannels.Count == 0
            ? embed.AddField("Canais de voz", BotConstants.NotSet, false)
            : embed.AddField("Canais de voz", string.Join("\n", guild.VoiceChannels.Select(v =>
            {
                var channel = Context.Guild.GetVoiceChannel(v.CreatorChannelId);
                var name = channel != null ? channel.Name : $"Canal removido ({v.CreatorChannelId})";
                var limit = v.UserLimit ?? VoiceChannelSettings.DefaultUserLimit;
                return $"{name} — {(v.Enabled ? "✅ ativo" : "⛔ desativado")} · limite {limit}";
            })), false);

        embed.WithStandardFooter("Use /config para ajustar");

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("boasvindas-canal", "Define o canal de boas-vindas e ativa as mensagens")]
    public async Task BoasVindasCanalAsync(
        [Summary("canal", "Canal de texto das boas-vindas")] ITextChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.WelcomeChannelId = canal.Id;
        guild.Welcome.WelcomeEnabled = true;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync($"✅ Canal de boas-vindas definido para {canal.Mention} e ativado!");
    }

    [SlashCommand("boasvindas-mensagem", "Define a mensagem de boas-vindas (variáveis: {user}, {server}, {count})")]
    public async Task BoasVindasMensagemAsync(
        [Summary("mensagem", "Texto da mensagem de boas-vindas")] string mensagem)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.WelcomeMessage = mensagem;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync("✅ Mensagem de boas-vindas atualizada!");
    }

    [SlashCommand("boasvindas-desativar", "Desativa as mensagens de boas-vindas")]
    public async Task BoasVindasDesativarAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.WelcomeEnabled = false;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync("✅ Mensagens de boas-vindas desativadas.");
    }

    [SlashCommand("boasvindas-exibir", "Mostra a configuração atual das boas-vindas")]
    public async Task BoasVindasExibirAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var embed = new EmbedBuilder()
            .WithTitle("👋 Configuração de Boas-vindas")
            .WithGoldTheme()
            .WithStatus("Status", guild.Welcome.WelcomeEnabled)
            .WithChannelField("Canal", guild.Welcome.WelcomeChannelId, Context.Guild)
            .AddField("Mensagem", guild.Welcome.WelcomeMessage, false)
            .WithFooter("Use {user}, {server}, {count} nas mensagens")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("despedidas-canal", "Define o canal de despedidas e ativa as mensagens")]
    public async Task DespedidasCanalAsync(
        [Summary("canal", "Canal de texto das despedidas")] ITextChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.GoodbyeChannelId = canal.Id;
        guild.Welcome.GoodbyeEnabled = true;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync($"✅ Canal de despedidas definido para {canal.Mention} e ativado!");
    }

    [SlashCommand("despedidas-mensagem", "Define a mensagem de despedida (variáveis: {user}, {server}, {count})")]
    public async Task DespedidasMensagemAsync(
        [Summary("mensagem", "Texto da mensagem de despedida")] string mensagem)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.GoodbyeMessage = mensagem;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync("✅ Mensagem de despedida atualizada!");
    }

    [SlashCommand("despedidas-desativar", "Desativa as mensagens de despedida")]
    public async Task DespedidasDesativarAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.GoodbyeEnabled = false;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync("✅ Mensagens de despedidas desativadas.");
    }

    [SlashCommand("voice-setup", "Adiciona/reativa um canal criador de voz")]
    public async Task VoiceSetupAsync(
        [Summary("canal", "Canal de voz que vira ponto de criação")] IVoiceChannel canal,
        [Summary("limite", "Limite de usuários nos canais criados (padrão 10)")] int? limite = null,
        [Summary("nome", "Modelo do nome dos canais criados (use {name})")] string? nome = null)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var settings = guild.VoiceChannels.FirstOrDefault(v => v.CreatorChannelId == canal.Id);
        if (settings == null)
        {
            settings = new VoiceChannelSettings { CreatorChannelId = canal.Id };
            guild.VoiceChannels.Add(settings);
        }

        settings.Enabled = true;
        if (limite is > 0)
            settings.UserLimit = limite;
        if (!string.IsNullOrWhiteSpace(nome))
            settings.NameTemplate = nome;

        await _guildRepository.SaveAsync(guild);

        await RespondAsync(
            $"✅ Canal criador `{canal.Name}` ativado!\n" +
            "Quando alguém entrar nele, o bot cria um canal de voz privado com o nome da pessoa.");
    }

    [SlashCommand("voice-desativar", "Desativa um canal criador de voz")]
    public async Task VoiceDesativarAsync(
        [Summary("canal", "Canal criador a desativar")] IVoiceChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var settings = guild.VoiceChannels.FirstOrDefault(v => v.CreatorChannelId == canal.Id);

        if (settings == null)
        {
            await RespondAsync($"❌ `{canal.Name}` não está configurado como canal criador.");
            return;
        }

        settings.Enabled = false;
        await _guildRepository.SaveAsync(guild);

        await RespondAsync($"✅ Canal criador `{canal.Name}` desativado.");
    }

    [SlashCommand("voice-remover", "Remove um canal criador de voz da configuração")]
    public async Task VoiceRemoverAsync(
        [Summary("canal", "Canal criador a remover")] IVoiceChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var removed = guild.VoiceChannels.RemoveAll(v => v.CreatorChannelId == canal.Id) > 0;
        if (!removed)
        {
            await RespondAsync($"❌ `{canal.Name}` não está configurado como canal criador.");
            return;
        }

        await _guildRepository.SaveAsync(guild);

        await RespondAsync($"✅ Canal criador `{canal.Name}` removido.");
    }

    [SlashCommand("voice-exibir", "Mostra a configuração atual dos canais de voz")]
    public async Task VoiceExibirAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.VoiceChannels.Count == 0)
        {
            await RespondAsync("Este servidor não tem canais criadores de voz configurados.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Canais de Voz")
            .WithGoldTheme()
            .WithDescription("Ao entrar num canal criador, o bot cria um canal de voz privado com o nome da pessoa.")
            .WithFooter("Use /config voice-setup para adicionar");

        foreach (var settings in guild.VoiceChannels)
        {
            var channel = Context.Guild.GetVoiceChannel(settings.CreatorChannelId);
            var name = settings.NameTemplate ?? VoiceChannelSettings.DefaultNameTemplate;
            var limit = settings.UserLimit ?? VoiceChannelSettings.DefaultUserLimit;

            embed = embed.AddField(
                channel != null ? channel.Name : $"Canal removido ({settings.CreatorChannelId})",
                $"Status: {(settings.Enabled ? "✅ ativo" : "⛔ desativado")}\n" +
                $"Nome padrão: `{name}`\n" +
                $"Limite: {limit}",
                true);
        }

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("prefixo-definir", "Define um novo prefixo de comandos para este servidor")]
    public async Task PrefixoDefinirAsync(
        [Summary("prefixo", "Novo prefixo (máx. 10 caracteres)")] string prefixo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var trimmed = prefixo.Trim();
        if (trimmed.Length == 0)
        {
            await RespondAsync("❌ Informe um prefixo válido.");
            return;
        }

        if (trimmed.Length > PrefixoMaxLength)
        {
            await RespondAsync($"❌ O prefixo deve ter no máximo **{PrefixoMaxLength}** caracteres.");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Prefix.Prefix = trimmed + " ";
        await _guildRepository.SaveAsync(guild);

        await RespondAsync(
            $"✅ Prefixo definido para `{trimmed}`!\n" +
            $"A partir de agora use `{trimmed}ajuda` para ver os comandos.");
    }

    [SlashCommand("prefixo-resetar", "Volta o prefixo deste servidor ao padrão global")]
    public async Task PrefixoResetarAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Prefix.Prefix = null;
        await _guildRepository.SaveAsync(guild);

        var defaultPrefix = _botOptions.Value.CommandPrefix;
        await RespondAsync($"✅ Prefixo resetado! Voltou ao padrão global: `{defaultPrefix}`");
    }

    [SlashCommand("prefixo-exibir", "Mostra o prefixo de comandos deste servidor")]
    public async Task PrefixoExibirAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var current = GetCurrentPrefix(guild);

        await RespondAsync($"⚙️ Prefixo de comandos deste servidor: `{current}`");
    }

    private const int PrefixoMaxLength = 10;

    private string GetCurrentPrefix(Guild guild)
        => !string.IsNullOrWhiteSpace(guild.Prefix.Prefix)
            ? guild.Prefix.Prefix
            : _botOptions.Value.CommandPrefix;
}