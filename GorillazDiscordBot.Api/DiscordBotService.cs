using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot;

public class DiscordBotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;
    private readonly InteractionService _interactions;
    private readonly IOptions<BotOptions> _botOptions;
    private readonly ILogger<DiscordBotService> _logger;
    private readonly IServiceProvider _services;
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IVoiceChannelService _voiceChannelService;
    private readonly IChatInteractionService _chatInteractionService;

    public DiscordBotService(
        DiscordSocketClient client,
        CommandService commands,
        InteractionService interactions,
        IOptions<BotOptions> botOptions,
        ILogger<DiscordBotService> logger,
        IServiceProvider services,
        ISettingsRepository<Guild> guildRepository,
        IVoiceChannelService voiceChannelService,
        IChatInteractionService chatInteractionService)
    {
        _client = client;
        _commands = commands;
        _interactions = interactions;
        _botOptions = botOptions;
        _logger = logger;
        _services = services;
        _guildRepository = guildRepository;
        _voiceChannelService = voiceChannelService;
        _chatInteractionService = chatInteractionService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += LogAsync;
        _commands.Log += LogAsync;
        _interactions.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteractionAsync;
        _client.MessageReceived += HandleCommandAsync;
        _client.UserJoined += OnUserJoinedAsync;
        _client.UserLeft += OnUserLeftAsync;
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;
        _client.GuildAvailable += RefreshGuildInfoAsync;
        _client.GuildUpdated += HandleGuildUpdatedAsync;

        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        var token = _botOptions.Value.DiscordToken;
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogCritical("DISCORD_TOKEN não configurado. Verifique o arquivo .env");
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.LogInformation("Bot iniciado com sucesso");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bot desligando...");

        _client.Log -= LogAsync;
        _commands.Log -= LogAsync;
        _interactions.Log -= LogAsync;
        _client.Ready -= ReadyAsync;
        _client.InteractionCreated -= HandleInteractionAsync;
        _client.MessageReceived -= HandleCommandAsync;
        _client.UserJoined -= OnUserJoinedAsync;
        _client.UserLeft -= OnUserLeftAsync;
        _client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;
        _client.GuildAvailable -= RefreshGuildInfoAsync;
        _client.GuildUpdated -= HandleGuildUpdatedAsync;

        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private Task LogAsync(LogMessage log)
    {
        _logger.LogInformation("[Discord] {message}", log.ToString());
        return Task.CompletedTask;
    }

    private bool _slashCommandsRegistered;

    private async Task ReadyAsync()
    {
        _logger.LogInformation(
            "Bot conectado como {username}#{discriminator}",
            _client.CurrentUser.Username,
            _client.CurrentUser.Discriminator);

        await RegisterSlashCommandsAsync();
    }

    private async Task RegisterSlashCommandsAsync()
    {
        if (_slashCommandsRegistered) return;
        _slashCommandsRegistered = true;

        try
        {
            var devGuildId = Environment.GetEnvironmentVariable("DISCORD_DEV_GUILD_ID");

            if (ulong.TryParse(devGuildId, out var guildId))
            {
                await _interactions.RegisterCommandsToGuildAsync(guildId);
                _logger.LogInformation("Slash commands registrados na guilda de teste {guildId}", guildId);
            }
            else
            {
                await _interactions.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Slash commands registrados globalmente (podem levar até 1h para propagar)");
            }
        }
        catch (Exception ex)
        {
            _slashCommandsRegistered = false;
            _logger.LogError(ex, "Falha ao registrar slash commands");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _interactions.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
                _logger.LogWarning("Erro ao executar interação '{id}': {error}",
                    interaction.Id, result.ErrorReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção ao processar interação {id}", interaction.Id);

            if (interaction.Type == InteractionType.ApplicationCommand)
                await interaction.RespondAsync("Ocorreu um erro inesperado.", ephemeral: true);
        }
    }

    private async Task HandleCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message) return;
        if (string.IsNullOrWhiteSpace(message.Content)) return;
        if (message.Author.IsBot) return;

        int argPos = 0;
        var prefix = await GetPrefixAsync(message);

        if (!message.HasStringPrefix(prefix, ref argPos, StringComparison.OrdinalIgnoreCase) &&
            !message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            return;

        var context = new SocketCommandContext(_client, message);
        var result = await _commands.ExecuteAsync(context, argPos, _services);

        if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
        {
            _logger.LogWarning("Erro ao executar comando '{command}': {error}",
                message.Content, result.ErrorReason);

            await context.Channel.SendMessageAsync($"Erro: {result.ErrorReason}");
            return;
        }

        if (!result.IsSuccess && result.Error == CommandError.UnknownCommand && context.Guild != null)
        {
            await _chatInteractionService.TryRespondAsync(context, prefix);
        }
    }

    private async Task<string> GetPrefixAsync(SocketUserMessage message)
    {
        if (message.Channel is SocketGuildChannel guildChannel)
        {
            var guild = await _guildRepository.GetAsync(guildChannel.Guild.Id);
            if (!string.IsNullOrWhiteSpace(guild.Prefix.Prefix))
                return guild.Prefix.Prefix;
        }

        return _botOptions.Value.CommandPrefix;
    }

    private async Task HandleGuildUpdatedAsync(SocketGuild before, SocketGuild after)
        => await RefreshGuildInfoAsync(after);

    private async Task RefreshGuildInfoAsync(SocketGuild socketGuild)
    {
        try
        {
            var guild = await _guildRepository.GetAsync(socketGuild.Id);
            UpdateGuildInfo(guild, socketGuild);
            await _guildRepository.SaveAsync(guild);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar informações do servidor {guild} ({guildId})",
                socketGuild.Name, socketGuild.Id);
        }
    }

    private static void UpdateGuildInfo(Guild guild, SocketGuild socketGuild)
    {
        guild.Info.Name = socketGuild.Name;
        guild.Info.IconUrl = socketGuild.IconUrl;
        guild.Info.OwnerId = socketGuild.OwnerId;
        guild.Info.OwnerName = socketGuild.Owner?.Username ?? guild.Info.OwnerName;
        guild.Info.MemberCount = socketGuild.MemberCount;
        guild.Info.BoostCount = socketGuild.PremiumSubscriptionCount;
        guild.Info.BoostLevel = (int)socketGuild.PremiumTier;
        guild.Info.JoinedAt ??= DateTime.UtcNow;
        guild.Info.CreatedAt ??= socketGuild.CreatedAt.UtcDateTime;
        guild.Info.PreferredLocale = socketGuild.PreferredLocale;
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            var guild = await _guildRepository.GetAsync(user.Guild.Id);
            UpdateGuildInfo(guild, user.Guild);
            await _guildRepository.SaveAsync(guild);

            if (!guild.Welcome.WelcomeEnabled || !guild.Welcome.WelcomeChannelId.HasValue)
                return;

            var channel = user.Guild.GetTextChannel(guild.Welcome.WelcomeChannelId.Value);
            if (channel == null) return;

            var message = MessageTemplateResolver.Resolve(
                guild.Welcome.WelcomeMessage,
                userMention: user.Mention,
                serverName: user.Guild.Name,
                memberCount: user.Guild.MemberCount);

            var embed = new EmbedBuilder()
                .WithTitle("🟢 Bem-vindo(a)!")
                .WithDescription(message)
                .WithColor(Color.Green)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithStandardFooter($"Membro nº {user.Guild.MemberCount}")
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar boas-vindas para {user} no servidor {guild}",
                user.GetDisplayName(), user.Guild.Name);
        }
    }

    private async Task OnUserLeftAsync(SocketGuild socketGuild, SocketUser user)
    {
        try
        {
            var guild = await _guildRepository.GetAsync(socketGuild.Id);
            UpdateGuildInfo(guild, socketGuild);
            await _guildRepository.SaveAsync(guild);

            if (!guild.Welcome.GoodbyeEnabled || !guild.Welcome.GoodbyeChannelId.HasValue)
                return;

            var channel = socketGuild.GetTextChannel(guild.Welcome.GoodbyeChannelId.Value);
            if (channel == null) return;

            var message = MessageTemplateResolver.Resolve(
                guild.Welcome.GoodbyeMessage,
                userMention: user.GetDisplayName(),
                serverName: socketGuild.Name,
                memberCount: socketGuild.MemberCount);

            var embed = new EmbedBuilder()
                .WithTitle("🔴 Adeus!")
                .WithDescription(message)
                .WithColor(Color.Red)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithStandardFooter($"Membros restantes: {socketGuild.MemberCount}")
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar despedida para {user} no servidor {guild}",
                user.GetDisplayName(), socketGuild.Name);
        }
    }

    private Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        => _voiceChannelService.OnUserVoiceStateUpdatedAsync(user, before, after);
}