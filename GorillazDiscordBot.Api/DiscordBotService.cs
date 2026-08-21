using System.Reflection;
using System.Runtime.InteropServices;
using Discord;
using Discord.Commands;
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
    private readonly IOptions<BotOptions> _botOptions;
    private readonly ILogger<DiscordBotService> _logger;
    private readonly IServiceProvider _services;
    private readonly ISettingsRepository<GuildWelcomeSettings> _welcomeRepository;
    private readonly ISettingsRepository<GuildPrefixSettings> _prefixRepository;
    private readonly IVoiceChannelService _voiceChannelService;
    private readonly IChatInteractionService _chatInteractionService;
    private readonly IVoicePresenceService _voicePresenceService;
    private readonly ISoundTriggerService _soundTriggerService;

    public DiscordBotService(
        DiscordSocketClient client,
        CommandService commands,
        IOptions<BotOptions> botOptions,
        ILogger<DiscordBotService> logger,
        IServiceProvider services,
        ISettingsRepository<GuildWelcomeSettings> welcomeRepository,
        ISettingsRepository<GuildPrefixSettings> prefixRepository,
        IVoiceChannelService voiceChannelService,
        IChatInteractionService chatInteractionService,
        IVoicePresenceService voicePresenceService,
        ISoundTriggerService soundTriggerService)
    {
        _client = client;
        _commands = commands;
        _botOptions = botOptions;
        _logger = logger;
        _services = services;
        _welcomeRepository = welcomeRepository;
        _prefixRepository = prefixRepository;
        _voiceChannelService = voiceChannelService;
        _chatInteractionService = chatInteractionService;
        _voicePresenceService = voicePresenceService;
        _soundTriggerService = soundTriggerService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        CheckAudioNativeLibraries();

        _client.Log += LogAsync;
        _commands.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.Connected += OnResumedAsync;
        _client.MessageReceived += HandleCommandAsync;
        _client.UserJoined += OnUserJoinedAsync;
        _client.UserLeft += OnUserLeftAsync;
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;

        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

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
        _client.Ready -= ReadyAsync;
        _client.Connected -= OnResumedAsync;
        _client.MessageReceived -= HandleCommandAsync;
        _client.UserJoined -= OnUserJoinedAsync;
        _client.UserLeft -= OnUserLeftAsync;
        _client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;

        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private void CheckAudioNativeLibraries()
    {
        foreach (var library in new[] { "libsodium", "opus" })
        {
            if (CanLoadNativeLibrary(library)) continue;

            _logger.LogWarning(
                "Biblioteca nativa '{library}' não encontrada. Os recursos de áudio/voz não funcionarão. " +
                "Em desenvolvimento Windows, coloque a DLL junto ao executável ou rode via Docker.",
                library);
        }
    }

    private static bool CanLoadNativeLibrary(string library)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { library }
            : new[]
            {
                library,
                $"{library}.so",
                $"{library}.so.0",
                $"{library}.so.1",
                $"{library}.so.23",
                $"lib{library}.so",
                $"lib{library}.so.0",
                $"lib{library}.so.1",
                $"lib{library}.so.23"
            };

        return candidates.Any(candidate => NativeLibrary.TryLoad(candidate, out _));
    }

    private Task LogAsync(LogMessage log)
    {
        _logger.LogInformation("[Discord] {message}", log.ToString());
        return Task.CompletedTask;
    }

    private Task ReadyAsync()
    {
        _logger.LogInformation(
            "Bot conectado como {username}#{discriminator}",
            _client.CurrentUser.Username,
            _client.CurrentUser.Discriminator);

        _ = RunReconnectAllAsync();
        return Task.CompletedTask;
    }

    private Task OnResumedAsync()
    {
        _ = RunReconnectAllAsync();
        return Task.CompletedTask;
    }

    private async Task RunReconnectAllAsync()
    {
        try
        {
            await _voicePresenceService.ReconnectAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reconectar canais de voz salvos");
        }
    }

    private Task HandleCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(message.Content)) return Task.CompletedTask;
        if (message.Author.IsBot) return Task.CompletedTask;

        _ = Task.Run(() => HandleCommandInternalAsync(message));

        return Task.CompletedTask;
    }

    private async Task HandleCommandInternalAsync(SocketUserMessage message)
    {
        try
        {
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
                if (await _chatInteractionService.TryRespondAsync(context, prefix))
                    return;

                await _soundTriggerService.TryPlayAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado ao processar mensagem '{content}'", message.Content);
        }
    }

    private async Task<string> GetPrefixAsync(SocketUserMessage message)
    {
        if (message.Channel is SocketGuildChannel guildChannel)
        {
            var settings = await _prefixRepository.GetAsync(guildChannel.Guild.Id);
            if (!string.IsNullOrWhiteSpace(settings.Prefix))
                return settings.Prefix;
        }

        return _botOptions.Value.CommandPrefix;
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            var settings = await _welcomeRepository.GetAsync(user.Guild.Id);

            if (!settings.WelcomeEnabled || !settings.WelcomeChannelId.HasValue)
                return;

            var channel = user.Guild.GetTextChannel(settings.WelcomeChannelId.Value);
            if (channel == null) return;

            var message = MessageTemplateResolver.Resolve(
                settings.WelcomeMessage,
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

    private async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        try
        {
            var settings = await _welcomeRepository.GetAsync(guild.Id);

            if (!settings.GoodbyeEnabled || !settings.GoodbyeChannelId.HasValue)
                return;

            var channel = guild.GetTextChannel(settings.GoodbyeChannelId.Value);
            if (channel == null) return;

            var message = MessageTemplateResolver.Resolve(
                settings.GoodbyeMessage,
                userMention: user.GetDisplayName(),
                serverName: guild.Name,
                memberCount: guild.MemberCount);

            var embed = new EmbedBuilder()
                .WithTitle("🔴 Adeus!")
                .WithDescription(message)
                .WithColor(Color.Red)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithStandardFooter($"Membros restantes: {guild.MemberCount}")
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar despedida para {user} no servidor {guild}",
                user.GetDisplayName(), guild.Name);
        }
    }

    private async Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        await _voiceChannelService.OnUserVoiceStateUpdatedAsync(user, before, after);
        await _voicePresenceService.OnBotVoiceStateUpdatedAsync(user, before, after);
    }
}
