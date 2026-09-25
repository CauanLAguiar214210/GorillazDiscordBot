using System.Linq;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Events;
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
    private readonly IChatInteractionService _chatInteractionService;
    private readonly ShopService _shopService;
    private readonly ReleaseAnnouncementService _releaseAnnouncementService;
    private readonly IEnumerable<IBotEventSink> _eventSinks;

    public DiscordBotService(
        DiscordSocketClient client,
        CommandService commands,
        InteractionService interactions,
        IOptions<BotOptions> botOptions,
        ILogger<DiscordBotService> logger,
        IServiceProvider services,
        ISettingsRepository<Guild> guildRepository,
        IChatInteractionService chatInteractionService,
        ShopService shopService,
        ReleaseAnnouncementService releaseAnnouncementService,
        IEnumerable<IBotEventSink> eventSinks)
    {
        _client = client;
        _commands = commands;
        _interactions = interactions;
        _botOptions = botOptions;
        _logger = logger;
        _services = services;
        _guildRepository = guildRepository;
        _chatInteractionService = chatInteractionService;
        _shopService = shopService;
        _releaseAnnouncementService = releaseAnnouncementService;
        _eventSinks = eventSinks;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += LogAsync;
        _commands.Log += LogAsync;
        _interactions.Log += LogAsync;
        _client.MessageReceived += HandleCommandAsync;
        _client.InteractionCreated += HandleInteractionAsync;
        _client.Ready += ReadyAsync;

        foreach (var sink in _eventSinks)
            sink.Subscribe(_client);

        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        var moduleTypes = Assembly.GetEntryAssembly()!.GetTypes()
            .Where(t => typeof(InteractionModuleBase<SocketInteractionContext>).IsAssignableFrom(t)
                        && t is { IsAbstract: false, IsInterface: false, DeclaringType: null });

        foreach (var type in moduleTypes)
        {
            if (type.GetCustomAttribute<DisabledAttribute>() != null)
            {
                _logger.LogInformation("Módulo desabilitado: {module}", type.Name);
                continue;
            }

            await _interactions.AddModuleAsync(type, _services);
        }

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
        _client.MessageReceived -= HandleCommandAsync;
        _client.InteractionCreated -= HandleInteractionAsync;
        _client.Ready -= ReadyAsync;

        foreach (var sink in _eventSinks)
            sink.Unsubscribe(_client);

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

        await SeedShopAsync();
        await RegisterSlashCommandsAsync();
        await AnnounceReleasesAsync();
    }

    private bool _releasesAnnounced;

    private async Task AnnounceReleasesAsync()
    {
        if (_releasesAnnounced) return;
        _releasesAnnounced = true;

        try
        {
            var announced = await _releaseAnnouncementService.AnnouncePendingReleasesAsync(_client.Guilds);
            if (announced > 0)
                _logger.LogInformation("Anúncios de novidades publicados no boot: {count}", announced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao anunciar releases pendentes.");
        }
    }

    private async Task SeedShopAsync()
    {
        try
        {
            await _shopService.SeedIfEmptyAsync();
            _logger.LogInformation("Catálogo da loja verificado (seed executado se necessário).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao executar seed da loja.");
        }
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

            if (interaction.Type is InteractionType.ApplicationCommand
                or InteractionType.MessageComponent
                or InteractionType.ModalSubmit)
            {
                try
                {
                    await interaction.RespondAsync("Ocorreu um erro inesperado.", ephemeral: true);
                }
                catch (Exception responseEx)
                {
                    _logger.LogWarning(responseEx, "Não foi possível responder ao erro da interação {id}", interaction.Id);
                }
            }
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
}