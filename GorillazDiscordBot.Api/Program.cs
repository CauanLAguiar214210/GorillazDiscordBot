using AWS.Logger;
using AWS.Logger.AspNetCore;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Domain.Policies;
using GorillazDiscordBot.Events;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Services.Interfaces;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
try { DotNetEnv.Env.Load(envPath); }
catch (FileNotFoundException) { }

var builder = Host.CreateApplicationBuilder(args);

// Options Pattern
builder.Services.Configure<BotOptions>(options =>
{
    options.DiscordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "";
    options.CommandPrefix = Environment.GetEnvironmentVariable("COMMAND_PREFIX") ?? "macaco ";
});

builder.Services.Configure<MongoOptions>(options =>
{
    options.ConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
    options.DatabaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") ?? "gorillazbot";
});

// Discord Socket Client (singleton)
builder.Services.AddSingleton<DiscordSocketClient>(_ =>
{
    var config = new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds
                       | GatewayIntents.GuildMessages
                       | GatewayIntents.MessageContent
                       | GatewayIntents.DirectMessages
                       | GatewayIntents.GuildMembers
                       | GatewayIntents.GuildVoiceStates,
        AlwaysDownloadUsers = true,
        MessageCacheSize = 100
    };
    return new DiscordSocketClient(config);
});

// Áudio — servidor Lavalink (toca sons no canal de voz e sai ao terminar).
// ⚠️ AddLavalink deve ser registrado APÓS o DiscordSocketClient.
builder.Services.Configure<LavalinkOptions>(options =>
{
    options.RestUri = Environment.GetEnvironmentVariable("LAVALINK_REST_URI") ?? LavalinkOptions.DefaultRestUri;
    options.WebSocketUri = Environment.GetEnvironmentVariable("LAVALINK_WS_URI") ?? LavalinkOptions.DefaultWebSocketUri;
    options.Passphrase = Environment.GetEnvironmentVariable("LAVALINK_PASSWORD") ?? LavalinkOptions.DefaultPassphrase;
    options.LocalAudioPath = Environment.GetEnvironmentVariable("LAVALINK_LOCAL_AUDIO_PATH") ?? LavalinkOptions.DefaultLocalAudioPath;
    options.InstantMirrorBaseUrl = Environment.GetEnvironmentVariable("AUDIO_INSTANT_MIRROR_BASE_URL") ?? LavalinkOptions.DefaultInstantMirrorBaseUrl;
});

builder.Services.AddLavalink();
builder.Services.ConfigureLavalink(options =>
{
    options.BaseAddress = new Uri(Environment.GetEnvironmentVariable("LAVALINK_REST_URI") ?? LavalinkOptions.DefaultRestUri);
    options.Passphrase = Environment.GetEnvironmentVariable("LAVALINK_PASSWORD") ?? LavalinkOptions.DefaultPassphrase;
    options.WebSocketUri = new Uri(Environment.GetEnvironmentVariable("LAVALINK_WS_URI") ?? LavalinkOptions.DefaultWebSocketUri);
});

builder.Services.AddSingleton<IAudioPlayerService, AudioPlayerService>();

// Sons instantâneos (Myinstants) — converte o link da página no MP3 direto
// que o Lavalink consegue tocar via source `http`.
builder.Services.AddHttpClient(InstantSoundResolver.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "GorillazDiscordBot/1.0");
});
builder.Services.AddSingleton<IInstantSoundResolver, InstantSoundResolver>();

// Command Service (singleton)
builder.Services.AddSingleton<CommandService>(new CommandService(new CommandServiceConfig
{
    DefaultRunMode = Discord.Commands.RunMode.Async,
    LogLevel = LogSeverity.Info
}));

// Interaction Service (slash commands, botões e select menus)
builder.Services.AddSingleton(sp =>
{
    var config = new InteractionServiceConfig
    {
        DefaultRunMode = Discord.Interactions.RunMode.Async,
        UseCompiledLambda = true,
        LogLevel = LogSeverity.Info,
        AutoServiceScopes = true
    };
    return new InteractionService(sp.GetRequiredService<DiscordSocketClient>(), config);
});

// Repository Pattern (MongoDB)
builder.Services.AddSingleton(typeof(IMongoRepository<>), typeof(MongoRepository<>));
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IEconomyRepository, EconomyRepository>();
builder.Services.AddSingleton<IShopRepository, ShopRepository>();
builder.Services.AddSingleton<IGifRepository, GifRepository>();
builder.Services.AddSingleton<IGuildMemberRepository, GuildMemberRepository>();
builder.Services.AddSingleton<IRankingRepository, RankingRepository>();
builder.Services.AddSingleton<ICharacterProfileRepository, CharacterProfileRepository>();
builder.Services.AddSingleton<IReleaseNoteRepository, ReleaseNoteRepository>();

// Guild settings (cache + MongoDB, um documento por servidor)
builder.Services.AddSingleton(typeof(ISettingsRepository<>), typeof(SettingsRepository<>));
builder.Services.AddSingleton<GuildSettingsAccessor>();
builder.Services.AddSingleton<IVoiceChannelService, VoiceChannelService>();

// Event sinks (handlers dedicados de eventos do Discord)
builder.Services.AddSingleton<IAltSanctionPolicy, GroupSanctionsPolicy>();
builder.Services.AddSingleton<IBotEventSink, GuildEventsSink>();

// Auto-moderação (palavras bloqueadas + proteção contra flood)
builder.Services.AddSingleton<StrikeEnforcementService>();
    builder.Services.AddSingleton<CommandPolicyService>();
    builder.Services.AddSingleton<AutoModService>();
builder.Services.AddSingleton<IBotEventSink>(sp => sp.GetRequiredService<AutoModService>());

// Log de servidor (auditoria de mensagens, membros e moderação)
builder.Services.AddSingleton<GuildLogService>();
    builder.Services.AddSingleton<IBotEventSink>(sp => sp.GetRequiredService<GuildLogService>());
    builder.Services.AddSingleton<RaidGuardService>();
    builder.Services.AddSingleton<IBotEventSink>(sp => sp.GetRequiredService<RaidGuardService>());

// Áudio ao entrar/sair de canais de voz (configurável por guilda)
builder.Services.AddSingleton<JoinLeaveSoundService>();
builder.Services.AddSingleton<IBotEventSink>(sp => sp.GetRequiredService<JoinLeaveSoundService>());

// Chat interactions por servidor (cache + MongoDB)
builder.Services.AddSingleton<IGuildInteractionRepository, GuildInteractionRepository>();
builder.Services.AddSingleton<IChatInteractionService, ChatInteractionService>();
builder.Services.AddHttpClient(ChatInteractionService.MediaHttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "GorillazDiscordBot/1.0");
});

// Contas vinculadas (alt accounts) + economia unificada
builder.Services.AddSingleton<IPrimaryAccountResolver, PrimaryAccountResolver>();
builder.Services.AddSingleton<IUserAccountService, UserAccountService>();

// Microserviço de cassino (sessões e jogos passam a viver no serviço)
builder.Services.AddHttpClient<CasinoApiClient>(client =>
{
    client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("LUCKY_MONKEY_URL")
        ?? "http://localhost:8080");
    client.Timeout = TimeSpan.FromSeconds(8);

    var apiKey = Environment.GetEnvironmentVariable("LUCKY_MONKEY_API_KEY");
    if (string.IsNullOrEmpty(apiKey))
    {
        Console.WriteLine("[AVISO] LUCKY_MONKEY_API_KEY não configurada — o serviço exigirá X-Api-Key.");
    }
    else
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CasinoJwtProvider.SigningKeyEnv)))
    {
        Console.WriteLine($"[AVISO] {CasinoJwtProvider.SigningKeyEnv} não configurada — o serviço de cassino exigirá JWT Bearer (token será emitido apenas quando a chave estiver presente).");
    }
});
builder.Services.AddSingleton<IWalletService, PayoutService>();
builder.Services.AddSingleton<CasinoBetTracker>();
builder.Services.AddSingleton<ShopService>();
builder.Services.AddSingleton<IShopService>(sp => sp.GetRequiredService<ShopService>());
builder.Services.AddSingleton<ReleaseAnnouncementService>();
builder.Services.AddSingleton<IPatrimonioService, PatrimonioService>();
builder.Services.AddSingleton<QuizSessionService>();
builder.Services.AddSingleton<JobExamSessionService>();
builder.Services.AddSingleton<LicencaExamSessionService>();
builder.Services.AddSingleton<InflationService>();
builder.Services.AddSingleton<ManobristaSessionService>();
builder.Services.AddSingleton<JobGameSessionService>();

// GIF URL Normalization
builder.Services.AddHttpClient<IGifUrlService, GifUrlService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "GorillazDiscordBot/1.0");
});

// CloudWatch Logs (opcional — ativar com env AWS_LOG_GROUP)
if (builder.Configuration.GetValue<string>("AWS_LOG_GROUP") is { Length: > 0 } logGroup)
{
    builder.Logging.AddAWSProvider(new AWSLoggerConfig
    {
        LogGroup = logGroup,
        Region = builder.Configuration.GetValue<string>("AWS_REGION") ?? "us-east-1"
    });
}

// Hosted Service (gerencia lifecycle do bot)
builder.Services.AddHostedService<DiscordBotService>();
builder.Services.AddHostedService<EconomyMaintenanceService>();
builder.Services.AddHostedService<ScheduledSoundService>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation(
    "Fuso de referência America/Sao_Paulo resolvido — offset UTC atual {offset} (esperado -03:00; 00:00 indica tzdata ausente no container)",
    ScheduleEvaluator.SaoPauloTimeZone.GetUtcOffset(DateTime.UtcNow));
try
{
    await host.Services.GetRequiredService<IGuildMemberRepository>().EnsureIndexesAsync();
    await host.Services.GetRequiredService<IUserRepository>().EnsureIndexesAsync();
    await host.Services.GetRequiredService<IShopRepository>().EnsureIndexesAsync();
    await host.Services.GetRequiredService<IRankingRepository>().EnsureIndexesAsync();
    await host.Services.GetRequiredService<ICharacterProfileRepository>().EnsureIndexesAsync();
    await host.Services.GetRequiredService<IReleaseNoteRepository>().EnsureIndexesAsync();
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Falha ao garantir índices das collections (GuildMember/DiscordUserProfile/Shop/Ranking/CharacterProfile/ReleaseNote)");
}

await host.RunAsync();
