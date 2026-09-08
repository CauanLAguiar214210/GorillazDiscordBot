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
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Services.Interfaces;
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

// Command Service (singleton)
builder.Services.AddSingleton<CommandService>();

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
builder.Services.AddSingleton<IGifRepository, GifRepository>();
builder.Services.AddSingleton<IGuildMemberRepository, GuildMemberRepository>();

// Guild settings (cache + MongoDB, um documento por servidor)
builder.Services.AddSingleton(typeof(ISettingsRepository<>), typeof(SettingsRepository<>));
builder.Services.AddSingleton<IVoiceChannelService, VoiceChannelService>();

// Chat interactions por servidor (cache + MongoDB)
builder.Services.AddSingleton<IGuildInteractionRepository, GuildInteractionRepository>();
builder.Services.AddSingleton<IChatInteractionService, ChatInteractionService>();

// Contas vinculadas (alt accounts) + economia unificada
builder.Services.AddSingleton<IEconomyAccessor, EconomyAccessor>();
builder.Services.AddSingleton<IUserAccountService, UserAccountService>();

// Sessões de jogos (memória)
builder.Services.AddSingleton<GameSessionManager>();
builder.Services.AddSingleton<CasinoSessionManager>();
builder.Services.AddSingleton<CasinoPlayService>();

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

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
try
{
    await host.Services.GetRequiredService<IGuildMemberRepository>().EnsureIndexesAsync();
    await host.Services.GetRequiredService<IUserRepository>().EnsureIndexesAsync();
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Falha ao garantir índices das collections GuildMember/DiscordUserProfile");
}

await host.RunAsync();
