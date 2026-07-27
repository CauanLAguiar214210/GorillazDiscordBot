using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Commands;

public class GuildModule : ModuleBase<SocketCommandContext>
{
    private readonly IGuildWelcomeRepository _welcomeRepository;

    public GuildModule(IGuildWelcomeRepository welcomeRepository)
    {
        _welcomeRepository = welcomeRepository;
    }

    [Command("welcome")]
    [Summary("Configura o canal de boas-vindas. Uso: macaco welcome #canal")]
    public async Task WelcomeAsync(ITextChannel? channel = null)
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco welcome #canal`");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);
        settings.WelcomeChannelId = channel.Id;
        settings.WelcomeEnabled = true;
        _welcomeRepository.Save(settings);

        await ReplyAsync($"✅ Canal de boas-vindas definido para {channel.Mention} e ativado!");
    }

    [Command("goodbye")]
    [Summary("Configura o canal de despedidas. Uso: macaco goodbye #canal")]
    public async Task GoodbyeAsync(ITextChannel? channel = null)
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco goodbye #canal`");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);
        settings.GoodbyeChannelId = channel.Id;
        settings.GoodbyeEnabled = true;
        _welcomeRepository.Save(settings);

        await ReplyAsync($"✅ Canal de despedidas definido para {channel.Mention} e ativado!");
    }

    [Command("welcomemsg")]
    [Summary("Define a mensagem de boas-vindas. Variáveis: {user}, {server}, {count}")]
    public async Task WelcomeMessageAsync([Remainder] string message)
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);
        settings.WelcomeMessage = message;
        _welcomeRepository.Save(settings);

        await ReplyAsync("✅ Mensagem de boas-vindas atualizada!");
    }

    [Command("goodbyemsg")]
    [Summary("Define a mensagem de despedida. Variáveis: {user}, {server}, {count}")]
    public async Task GoodbyeMessageAsync([Remainder] string message)
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);
        settings.GoodbyeMessage = message;
        _welcomeRepository.Save(settings);

        await ReplyAsync("✅ Mensagem de despedida atualizada!");
    }

    [Command("welcome off")]
    [Summary("Desativa as mensagens de boas-vindas")]
    public async Task WelcomeOffAsync()
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);
        settings.WelcomeEnabled = false;
        _welcomeRepository.Save(settings);

        await ReplyAsync("✅ Mensagens de boas-vindas desativadas.");
    }

    [Command("goodbye off")]
    [Summary("Desativa as mensagens de despedida")]
    public async Task GoodbyeOffAsync()
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);
        settings.GoodbyeEnabled = false;
        _welcomeRepository.Save(settings);

        await ReplyAsync("✅ Mensagens de despedida desativadas.");
    }

    [Command("welcome config")]
    [Summary("Mostra a configuração atual de boas-vindas e despedidas")]
    public async Task WelcomeConfigAsync()
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = _welcomeRepository.Get(Context.Guild.Id);

        var welcomeStatus = settings.WelcomeEnabled ? "🟢 Ativado" : "🔴 Desativado";
        var goodbyeStatus = settings.GoodbyeEnabled ? "🟢 Ativado" : "🔴 Desativado";
        var welcomeChannel = settings.WelcomeChannelId.HasValue
            ? $"<#{settings.WelcomeChannelId.Value}>"
            : "Não definido";
        var goodbyeChannel = settings.GoodbyeChannelId.HasValue
            ? $"<#{settings.GoodbyeChannelId.Value}>"
            : "Não definido";

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Boas-vindas & Despedidas")
            .WithColor(Color.Gold)
            .AddField("Boas-vindas", welcomeStatus, true)
            .AddField("Canal", welcomeChannel, true)
            .AddField("Mensagem", settings.WelcomeMessage, false)
            .AddField("Despedidas", goodbyeStatus, true)
            .AddField("Canal", goodbyeChannel, true)
            .AddField("Mensagem", settings.GoodbyeMessage, false)
            .WithFooter("Use {user}, {server}, {count} nas mensagens")
            .Build();

        await ReplyAsync(embed: embed);
    }

    private bool HasPermission()
        => Context.Guild != null &&
           Context.User is IGuildUser guildUser &&
           (guildUser.GuildPermissions.ManageGuild || guildUser.GuildPermissions.Administrator);
}
