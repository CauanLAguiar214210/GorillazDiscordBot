using AWS.Logger;
using AWS.Logger.SeriLog;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Repository;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
try { DotNetEnv.Env.Load(envPath); }
catch (FileNotFoundException) { }

var logLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Information";
var logLevel = Enum.TryParse<LogEventLevel>(logLevelEnv, true, out var parsed)
    ? parsed
    : LogEventLevel.Information;

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/bot-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

if (Environment.GetEnvironmentVariable("AWS_LOG_GROUP") is { Length: > 0 } logGroup)
{
    var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
    loggerConfig.WriteTo.AWSSeriLog(new AWSLoggerConfig
    {
        LogGroup = logGroup,
        Region = region
    });
}

Log.Logger = loggerConfig.CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog();

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
                       | GatewayIntents.GuildMembers,
        AlwaysDownloadUsers = true,
        MessageCacheSize = 100
    };
    return new DiscordSocketClient(config);
});

// Command Service (singleton)
builder.Services.AddSingleton<CommandService>();

// HTTP Clients via IHttpClientFactory (typed)
builder.Services.AddHttpClient<IFOneService, FOneService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<ICotacaoService, CotacaoService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Repository Pattern (MongoDB)
builder.Services.AddSingleton(typeof(IMongoRepository<>), typeof(MongoRepository<>));
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IGifRepository, GifRepository>();

// Welcome & Goodbye (in-memory)
builder.Services.AddSingleton<IGuildWelcomeRepository, GuildWelcomeRepository>();

// Weather Service (OpenWeatherMap)
builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// GIF URL Normalization
builder.Services.AddHttpClient<IGifUrlService, GifUrlService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "GorillazDiscordBot/1.0");
});

// Hosted Service (gerencia lifecycle do bot)
builder.Services.AddHostedService<DiscordBotService>();

var host = builder.Build();
await host.RunAsync();
