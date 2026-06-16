using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

class Program
{
    private DiscordSocketClient _client;
    private CommandService _commands;

    static async Task Main(string[] args)
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        try { DotNetEnv.Env.Load(envPath); }
        catch (FileNotFoundException) { }
        var program = new Program();
        await program.MainAsync();
    }

    public async Task MainAsync()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                             | GatewayIntents.GuildMessages
                             | GatewayIntents.MessageContent
                             | GatewayIntents.DirectMessages
        };

        _client = new DiscordSocketClient(config);
        _commands = new CommandService();

        _client.Log += LogAsync;
        _commands.Log += LogAsync;
        _client.Ready += ReadyAsync;

        await RegisterCommandsAsync();

        string token = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? "";
        string mongoConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";
        string mongoDatabaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE_NAME") ?? "gorillazbot";

        var mongoService = new MongoDBService(mongoConnectionString, mongoDatabaseName);

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
        await Task.Delay(-1);
    }

    private Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log.ToString());
        return Task.CompletedTask;
    }

    private Task ReadyAsync()
    {
        Console.WriteLine($"Bot conectado como {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        return Task.CompletedTask;
    }

    public async Task RegisterCommandsAsync()
    {
        _client.MessageReceived += HandleCommandAsync;
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
    }

    private async Task HandleCommandAsync(SocketMessage arg)
    {
        var message = arg as SocketUserMessage;

        if (message == null) return;        

        if (string.IsNullOrWhiteSpace(message.Content)) return;
        
        if (message.Author.IsBot) return;

        int argPos = 0;

        // Prefixo '!' para comandos
        if (!(message.HasStringPrefix("macaco ", ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)))
            return;

        var context = new SocketCommandContext(_client, message);

        // Executa o comando
        var result = await _commands.ExecuteAsync(context, argPos, null);

        if (!result.IsSuccess)
        {
            Console.WriteLine($"Erro ao executar comando: {result.ErrorReason}");
            await context.Channel.SendMessageAsync($"Erro: {result.ErrorReason}");
        }
    }
}
