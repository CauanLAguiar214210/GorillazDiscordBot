using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class GuildModule : ModuleBase<SocketCommandContext>
{
    private readonly ISettingsRepository<GuildWelcomeSettings> _welcomeRepository;

    public GuildModule(ISettingsRepository<GuildWelcomeSettings> welcomeRepository)
    {
        _welcomeRepository = welcomeRepository;
    }

    [Command("welcome")]
    [Summary("Configura o canal de boas-vindas. Uso: macaco welcome #canal")]
    public async Task WelcomeAsync(ITextChannel? channel = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco welcome #canal`");
            return;
        }

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);
        settings.WelcomeChannelId = channel.Id;
        settings.WelcomeEnabled = true;
        await _welcomeRepository.SaveAsync(settings);

        await ReplyAsync($"✅ Canal de boas-vindas definido para {channel.Mention} e ativado!");
    }

    [Command("goodbye")]
    [Summary("Configura o canal de despedidas. Uso: macaco goodbye #canal")]
    public async Task GoodbyeAsync(ITextChannel? channel = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco goodbye #canal`");
            return;
        }

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);
        settings.GoodbyeChannelId = channel.Id;
        settings.GoodbyeEnabled = true;
        await _welcomeRepository.SaveAsync(settings);

        await ReplyAsync($"✅ Canal de despedidas definido para {channel.Mention} e ativado!");
    }

    [Command("welcomemsg")]
    [Summary("Define a mensagem de boas-vindas. Variáveis: {user}, {server}, {count}")]
    public async Task WelcomeMessageAsync([Remainder] string message)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);
        settings.WelcomeMessage = message;
        await _welcomeRepository.SaveAsync(settings);

        await ReplyAsync("✅ Mensagem de boas-vindas atualizada!");
    }

    [Command("goodbyemsg")]
    [Summary("Define a mensagem de despedida. Variáveis: {user}, {server}, {count}")]
    public async Task GoodbyeMessageAsync([Remainder] string message)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);
        settings.GoodbyeMessage = message;
        await _welcomeRepository.SaveAsync(settings);

        await ReplyAsync("✅ Mensagem de despedida atualizada!");
    }

    [Command("welcome off")]
    [Summary("Desativa as mensagens de boas-vindas")]
    public async Task WelcomeOffAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);
        settings.WelcomeEnabled = false;
        await _welcomeRepository.SaveAsync(settings);

        await ReplyAsync("✅ Mensagens de boas-vindas desativadas.");
    }

    [Command("goodbye off")]
    [Summary("Desativa as mensagens de despedida")]
    public async Task GoodbyeOffAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);
        settings.GoodbyeEnabled = false;
        await _welcomeRepository.SaveAsync(settings);

        await ReplyAsync("✅ Mensagens de despedida desativadas.");
    }

    [Command("welcome config")]
    [Summary("Mostra a configuração atual de boas-vindas e despedidas")]
    public async Task WelcomeConfigAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _welcomeRepository.GetAsync(Context.Guild.Id);

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Boas-vindas & Despedidas")
            .WithGoldTheme()
            .WithStatus("Boas-vindas", settings.WelcomeEnabled)
            .WithChannelField("Canal", settings.WelcomeChannelId, Context.Guild)
            .AddField("Mensagem", settings.WelcomeMessage, false)
            .WithStatus("Despedidas", settings.GoodbyeEnabled)
            .WithChannelField("Canal", settings.GoodbyeChannelId, Context.Guild)
            .AddField("Mensagem", settings.GoodbyeMessage, false)
            .WithFooter("Use {user}, {server}, {count} nas mensagens")
            .Build();

        await ReplyAsync(embed: embed);
    }
}
