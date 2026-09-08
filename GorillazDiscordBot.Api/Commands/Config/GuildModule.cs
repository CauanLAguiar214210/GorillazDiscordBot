using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Config;

public class GuildModule : ModuleBase<SocketCommandContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public GuildModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
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

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.WelcomeChannelId = channel.Id;
        guild.Welcome.WelcomeEnabled = true;
        await _guildRepository.SaveAsync(guild);

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

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.GoodbyeChannelId = channel.Id;
        guild.Welcome.GoodbyeEnabled = true;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync($"✅ Canal de despedidas definido para {channel.Mention} e ativado!");
    }

    [Command("welcomemsg")]
    [Summary("Define a mensagem de boas-vindas. Variáveis: {user}, {server}, {count}")]
    public async Task WelcomeMessageAsync([Remainder] string message)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.WelcomeMessage = message;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync("✅ Mensagem de boas-vindas atualizada!");
    }

    [Command("goodbyemsg")]
    [Summary("Define a mensagem de despedida. Variáveis: {user}, {server}, {count}")]
    public async Task GoodbyeMessageAsync([Remainder] string message)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.GoodbyeMessage = message;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync("✅ Mensagem de despedida atualizada!");
    }

    [Command("welcome off")]
    [Summary("Desativa as mensagens de boas-vindas")]
    public async Task WelcomeOffAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.WelcomeEnabled = false;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync("✅ Mensagens de boas-vindas desativadas.");
    }

    [Command("goodbye off")]
    [Summary("Desativa as mensagens de despedida")]
    public async Task GoodbyeOffAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome.GoodbyeEnabled = false;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync("✅ Mensagens de despedida desativadas.");
    }

    [Command("welcome config")]
    [Summary("Mostra a configuração atual de boas-vindas e despedidas")]
    public async Task WelcomeConfigAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Boas-vindas & Despedidas")
            .WithGoldTheme()
            .WithStatus("Boas-vindas", guild.Welcome.WelcomeEnabled)
            .WithChannelField("Canal", guild.Welcome.WelcomeChannelId, Context.Guild)
            .AddField("Mensagem", guild.Welcome.WelcomeMessage, false)
            .WithStatus("Despedidas", guild.Welcome.GoodbyeEnabled)
            .WithChannelField("Canal", guild.Welcome.GoodbyeChannelId, Context.Guild)
            .AddField("Mensagem", guild.Welcome.GoodbyeMessage, false)
            .WithFooter("Use {user}, {server}, {count} nas mensagens")
            .Build();

        await ReplyAsync(embed: embed);
    }
}
