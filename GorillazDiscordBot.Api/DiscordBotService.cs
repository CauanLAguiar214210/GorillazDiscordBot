using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
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
    private readonly IVoiceChannelService _voiceChannelService;

    public DiscordBotService(
        DiscordSocketClient client,
        CommandService commands,
        IOptions<BotOptions> botOptions,
        ILogger<DiscordBotService> logger,
        IServiceProvider services,
        ISettingsRepository<GuildWelcomeSettings> welcomeRepository,
        IVoiceChannelService voiceChannelService)
    {
        _client = client;
        _commands = commands;
        _botOptions = botOptions;
        _logger = logger;
        _services = services;
        _welcomeRepository = welcomeRepository;
        _voiceChannelService = voiceChannelService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += LogAsync;
        _commands.Log += LogAsync;
        _client.Ready += ReadyAsync;
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
        _client.MessageReceived -= HandleCommandAsync;
        _client.UserJoined -= OnUserJoinedAsync;
        _client.UserLeft -= OnUserLeftAsync;
        _client.UserVoiceStateUpdated -= OnUserVoiceStateUpdatedAsync;

        await _client.StopAsync();
        await _client.LogoutAsync();
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

        return Task.CompletedTask;
    }

    private async Task HandleCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message) return;
        if (string.IsNullOrWhiteSpace(message.Content)) return;
        if (message.Author.IsBot) return;

        int argPos = 0;
        var prefix = _botOptions.Value.CommandPrefix;

        if (!message.HasStringPrefix(prefix, ref argPos) &&
            !message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            return;

        var context = new SocketCommandContext(_client, message);
        var result = await _commands.ExecuteAsync(context, argPos, _services);

        if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
        {
            _logger.LogWarning("Erro ao executar comando '{command}': {error}",
                message.Content, result.ErrorReason);

            await context.Channel.SendMessageAsync($"Erro: {result.ErrorReason}");
        }
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

            var message = settings.WelcomeMessage
                .Replace("{user}", user.Mention)
                .Replace("{server}", user.Guild.Name)
                .Replace("{count}", user.Guild.MemberCount.ToString());

            var embed = new EmbedBuilder()
                .WithTitle("🟢 Bem-vindo(a)!")
                .WithDescription(message)
                .WithColor(Color.Green)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithFooter($"Membro nº {user.Guild.MemberCount}")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar boas-vindas para {user} no servidor {guild}",
                user.Username, user.Guild.Name);
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

            var message = settings.GoodbyeMessage
                .Replace("{user}", user.Username)
                .Replace("{server}", guild.Name)
                .Replace("{count}", guild.MemberCount.ToString());

            var embed = new EmbedBuilder()
                .WithTitle("🔴 Adeus!")
                .WithDescription(message)
                .WithColor(Color.Red)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithFooter($"Membros restantes: {guild.MemberCount}")
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar despedida para {user} no servidor {guild}",
                user.Username, guild.Name);
        }
    }

    private Task OnUserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        => _voiceChannelService.OnUserVoiceStateUpdatedAsync(user, before, after);
}