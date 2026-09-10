using Discord;

namespace GorillazDiscordBot.Utils;

public static class CommandCatalog
{
    public enum CommandKind { Slash, Prefix, Both }

    public sealed record CommandEntry(string Name, string Description, CommandKind Kind);

    public sealed record Category(string Id, string Emoji, string Title, IReadOnlyList<CommandEntry> Commands);

    public static IReadOnlyList<Category> Categories { get; } = new List<Category>
    {
        new("diversao", "🎮", "Diversão", new[]
        {
            new CommandEntry("/ping", "Responde com Pong!", CommandKind.Slash),
            new CommandEntry("8ball <pergunta>", "Bola 8 mágica responde sua pergunta", CommandKind.Prefix),
            new CommandEntry("gorila", "Curiosidade sobre gorilas", CommandKind.Prefix),
            new CommandEntry("dado", "Joga um dado de 6 lados", CommandKind.Prefix),
            new CommandEntry("flip", "Cara ou coroa", CommandKind.Prefix),
        }),
        new("economia", "💰", "Economia", new[]
        {
            new CommandEntry("daily", "Reivindica moedas diárias", CommandKind.Prefix),
            new CommandEntry("saldo", "Ver seu saldo (alias: carteira)", CommandKind.Prefix),
            new CommandEntry("pagar <usuário> <valor>", "Transferir moedas para outro usuário", CommandKind.Prefix),
            new CommandEntry("depositar <valor>", "Move moedas da carteira para o banco", CommandKind.Prefix),
            new CommandEntry("sacar <valor>", "Move moedas do banco para a carteira", CommandKind.Prefix),
            new CommandEntry("banco", "Consulta seu banco", CommandKind.Prefix),
            new CommandEntry("poupar <valor>", "Deposita na poupança com juros diários", CommandKind.Prefix),
            new CommandEntry("resgatar <valor>", "Saca moedas da poupança", CommandKind.Prefix),
            new CommandEntry("trabalhar [serviço]", "Trabalha em um serviço e ganha moedas", CommandKind.Prefix),
            new CommandEntry("roubar <usuário>", "Tenta roubar moedas de outro usuário", CommandKind.Prefix),
            new CommandEntry("historico [n]", "Mostra o histórico de transações", CommandKind.Prefix),
            new CommandEntry("ranking", "Ranking de riqueza com classes e Ral da Fama", CommandKind.Prefix),
            new CommandEntry("raldafama", "As lendas do servidor com títulos e frases", CommandKind.Prefix),
            new CommandEntry("tiers", "Lista as classificações do ranking", CommandKind.Prefix),
        }),
        new("jogos", "🃏", "Jogos", new[]
        {
            new CommandEntry("/blackjack <valor>", "Inicia uma mão de Blackjack com botões", CommandKind.Slash),
        }),
        new("cassino", "🎰", "Cassino", new[]
        {
            new CommandEntry("/roleta <valor>", "Aposta na roleta com botões (número, cor, par/ímpar, metade)", CommandKind.Slash),
            new CommandEntry("/cacaniquel <valor>", "Caça-níquel com botões", CommandKind.Slash),
            new CommandEntry("/dados <valor> <tipo>", "Aposta nos dados (alta, baixa, sete, dupla)", CommandKind.Slash),
            new CommandEntry("/caraoucoroa <valor> <lado>", "Aposta em cara ou coroa", CommandKind.Slash),
            new CommandEntry("/aviaozinho <valor>", "Aposte no aviaozinho botão por botão", CommandKind.Slash),
            new CommandEntry("/poker <valor>", "Poker de máquina (Jacks or Better)", CommandKind.Slash),
            new CommandEntry("/minas <valor> [minas]", "Revela células seguras antes de achar uma mina", CommandKind.Slash),
            new CommandEntry("/limbo <valor> <alvo>", "El número sorteado que pasa del objetivo multiplica el valor", CommandKind.Slash),
            new CommandEntry("/jokenpo <valor> <jogada>", "Pedra, papel e tesoura valendo moedas", CommandKind.Slash),
            new CommandEntry("/corrida <valor> <cavalo>", "Aposta no cavalo que vai vencer a corrida", CommandKind.Slash),
            new CommandEntry("/altobaixo <valor>", "Acerte se a próxima carta é maior ou menor", CommandKind.Slash),
            new CommandEntry("/baccarat <valor> <aposta>", "Aposte no jogador, no banco ou no empate", CommandKind.Slash),
        }),
        new("loja", "🛒", "Loja", new[]
        {
            new CommandEntry("/loja [categoria]", "Mostra os itens disponíveis na loja (filtro por categoria)", CommandKind.Slash),
            new CommandEntry("/comprar <id>", "Compra um item da loja", CommandKind.Slash),
            new CommandEntry("/usar <id>", "Ativa um boost comprado", CommandKind.Slash),
            new CommandEntry("/inventario", "Mostra seus itens", CommandKind.Slash),
            new CommandEntry("/vender <id>", "Revende um item por reembolso parcial", CommandKind.Slash),
            new CommandEntry("/equipar <id>", "Equipa um relógio (bônus no cassino)", CommandKind.Slash),
            new CommandEntry("/desequipar <id>", "Desequipa o relógio ativo", CommandKind.Slash),
        }),
        new("utilidade", "🛠️", "Utilidades", new[]
        {
            new CommandEntry("userinfo", "Suas informações de usuário", CommandKind.Prefix),
            new CommandEntry("random <min> <max>", "Número aleatório entre min e max", CommandKind.Prefix),
            new CommandEntry("timer <segundos>", "Temporizador com aviso", CommandKind.Prefix),
            new CommandEntry("avatar [usuário]", "Avatar de um usuário", CommandKind.Prefix),
            new CommandEntry("serverinfo", "Informações do servidor", CommandKind.Prefix),
            new CommandEntry("horario", "Hora atual (UTC)", CommandKind.Prefix),
            new CommandEntry("contador <n>", "Conta de 1 até N", CommandKind.Prefix),
            new CommandEntry("reversa <texto>", "Inverte o texto informado", CommandKind.Prefix),
        }),
        new("contas", "👥", "Contas Vinculadas", new[]
        {
            new CommandEntry("contas", "Mostra suas contas vinculadas", CommandKind.Prefix),
            new CommandEntry("contas vincular <@alt>", "Declara uma alt sua (a alt recebe um código por DM)", CommandKind.Prefix),
            new CommandEntry("contas confirmar <código>", "Confirma o vínculo usando o código recebido por DM", CommandKind.Prefix),
            new CommandEntry("contas desvincular <conta>", "Remove uma conta do seu grupo", CommandKind.Prefix),
        }),
        new("gifs", "🖼️", "GIFs", new[]
        {
            new CommandEntry("gif <nome>", "Envia o GIF salvo com esse nome", CommandKind.Prefix),
            new CommandEntry("gif add <nome> <url>", "Salva um GIF no servidor", CommandKind.Prefix),
            new CommandEntry("gif random", "Sorteia um GIF salvo", CommandKind.Prefix),
        }),
        new("interacoes", "💬", "Interações", new[]
        {
            new CommandEntry("interaction add <trigger> <resposta>", "Adiciona uma interação automática do servidor", CommandKind.Prefix),
            new CommandEntry("interaction remove <trigger>", "Remove uma interação do servidor", CommandKind.Prefix),
            new CommandEntry("interaction list", "Lista as interações do servidor", CommandKind.Prefix),
        }),
        new("config", "⚙️", "Configuração", new[]
        {
            new CommandEntry("/config status", "Visão geral da configuração do servidor", CommandKind.Slash),
            new CommandEntry("/config boasvindas-canal", "Define o canal de boas-vindas e ativa", CommandKind.Slash),
            new CommandEntry("/config boasvindas-mensagem", "Define a mensagem de boas-vindas", CommandKind.Slash),
            new CommandEntry("/config boasvindas-desativar", "Desativa as mensagens de boas-vindas", CommandKind.Slash),
            new CommandEntry("/config boasvindas-exibir", "Mostra a configuração das boas-vindas", CommandKind.Slash),
            new CommandEntry("/config despedidas-canal", "Define o canal de despedidas e ativa", CommandKind.Slash),
            new CommandEntry("/config despedidas-mensagem", "Define a mensagem de despedida", CommandKind.Slash),
            new CommandEntry("/config despedidas-desativar", "Desativa as mensagens de despedida", CommandKind.Slash),
            new CommandEntry("/config voice-setup", "Adiciona/reativa um canal criador de voz", CommandKind.Slash),
            new CommandEntry("/config voice-desativar", "Desativa um canal criador de voz", CommandKind.Slash),
            new CommandEntry("/config voice-remover", "Remove um canal criador da configuração", CommandKind.Slash),
            new CommandEntry("/config voice-exibir", "Mostra a configuração dos canais de voz", CommandKind.Slash),
            new CommandEntry("/config prefixo-definir", "Define um novo prefixo de comandos", CommandKind.Slash),
            new CommandEntry("/config prefixo-resetar", "Volta o prefixo ao padrão global", CommandKind.Slash),
            new CommandEntry("/config prefixo-exibir", "Mostra o prefixo de comandos atual", CommandKind.Slash),
            new CommandEntry("welcome <canal>", "Configura o canal de boas-vindas (prefixo)", CommandKind.Prefix),
            new CommandEntry("welcomemsg <texto>", "Define a mensagem de boas-vindas (prefixo)", CommandKind.Prefix),
            new CommandEntry("welcome off", "Desativa boas-vindas (prefixo)", CommandKind.Prefix),
            new CommandEntry("goodbye <canal>", "Configura o canal de despedidas (prefixo)", CommandKind.Prefix),
            new CommandEntry("goodbyemsg <texto>", "Define a mensagem de despedida (prefixo)", CommandKind.Prefix),
            new CommandEntry("goodbye off", "Desativa despedidas (prefixo)", CommandKind.Prefix),
            new CommandEntry("voice setup <canal>", "Canal criador de voz (prefixo)", CommandKind.Prefix),
            new CommandEntry("voice off <canal>", "Desativa canal criador (prefixo)", CommandKind.Prefix),
            new CommandEntry("voice config", "Configuração dos canais de voz (prefixo)", CommandKind.Prefix),
            new CommandEntry("prefix [set|reset]", "Prefixo do bot neste servidor (prefixo)", CommandKind.Prefix),
        }),
    };

    public static Embed BuildOverviewEmbed(IUser botUser)
    {
        var slashCount = Categories.SelectMany(c => c.Commands).Count(c => c.Kind is CommandKind.Slash or CommandKind.Both);
        var prefixCount = Categories.SelectMany(c => c.Commands).Count(c => c.Kind is CommandKind.Prefix or CommandKind.Both);

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{botUser.Username} — Comandos", botUser.GetAvatarUrl())
            .WithTitle("Central de Ajuda")
            .WithDescription(
                "Selecione uma categoria no menu abaixo para ver todos os comandos.\n\n" +
                $"📊 **{slashCount}** comandos disponíveis por `/` (slash) — " +
                $"**{prefixCount}** disponíveis por prefixo.\n\n" +
                "💡 **Como usar:**\n" +
                "• Slash: `/comando` — aparecem ao digitar `/` no chat\n" +
                "• Prefixo: `{prefixo}comando` — digite o prefixo do servidor seguido do nome\n" +
                "• Mencione o bot: `@bot comando` — funciona sempre independentemente do prefixo");

        embed.WithStandardFooter("Clique no menu para navegar entre as categorias");
        return embed.Build();
    }

    public static Embed BuildCategoryEmbed(string categoryId, IUser botUser)
    {
        var category = Categories.FirstOrDefault(c => c.Id == categoryId);

        if (category == null)
            return BuildOverviewEmbed(botUser);

        var sb = new System.Text.StringBuilder();
        foreach (var cmd in category.Commands)
        {
            var badge = cmd.Kind switch
            {
                CommandKind.Slash => "`/`",
                CommandKind.Prefix => "`!`",
                CommandKind.Both => "`/!`",
                _ => ""
            };
            sb.AppendLine($"{badge} `{cmd.Name}` — {cmd.Description}");
        }

        return new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{botUser.Username} — Comandos", botUser.GetAvatarUrl())
            .WithTitle($"{category.Emoji} {category.Title}")
            .WithDescription(sb.ToString())
            .Build();
    }

    public static MessageComponent BuildSelectMenu(ulong invokerId)
    {
        var options = Categories
            .Select(c => new SelectMenuOptionBuilder($"{c.Emoji} {c.Title}", c.Id))
            .ToList();

        return new ComponentBuilder()
            .WithSelectMenu($"help:{invokerId}", options, "Escolha uma categoria…")
            .Build();
    }
}
