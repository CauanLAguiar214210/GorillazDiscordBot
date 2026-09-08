using Discord;

namespace GorillazDiscordBot.Utils;

public static class CommandCatalog
{
    public sealed record CommandEntry(string Name, string Description);

    public sealed record Category(string Id, string Emoji, string Title, IReadOnlyList<CommandEntry> Commands);

    /// <summary>Nomes de exibição dos módulos usados no comando de ajuda por prefixo.</summary>
    public static readonly Dictionary<string, (string Name, string Emoji)> ModuleDisplay = new()
    {
        ["FunModule"] = ("Diversão", "🎮"),
        ["UtilityModule"] = ("Utilidades", "🛠️"),
        ["EconomyModule"] = ("Economia", "💰"),
        ["GifManageModule"] = ("GIFs", "🖼️"),
        ["GuildModule"] = ("Boas-vindas & Despedidas", "👋"),
        ["VoiceModule"] = ("Canais de Voz", "🔊"),
        ["InteractionModule"] = ("Interações", "💬"),
        ["BlackjackModule"] = ("Blackjack", "🃏"),
        ["CasinoModule"] = ("Cassino", "🎰"),
        ["ShopModule"] = ("Loja", "🛒"),
    };

    /// <summary>Descrições dos comandos (chave = primeiro alias).</summary>
    public static readonly Dictionary<string, string> Descriptions = new()
    {
        ["ping"] = "Responde com Pong!",
        ["8ball"] = "Bola 8 mágica responde sua pergunta",
        ["gorila"] = "Curiosidade sobre gorilas",
        ["dado"] = "Joga um dado de 6 lados",
        ["flip"] = "Cara ou coroa",
        ["daily"] = "Reivindica moedas diárias",
        ["saldo"] = "Ver seu saldo (alias: coins)",
        ["blackjack"] = "Inicia uma mão de Blackjack (alias: bj)",
        ["hit"] = "Pede mais uma carta no Blackjack (alias: pedir)",
        ["stand"] = "Para e encerra a mão de Blackjack (alias: parar)",
        ["double"] = "Dobra a aposta no Blackjack (alias: dobrar)",
        ["roleta"] = "Aposta na roleta (número, cor, par/ímpar ou metade)",
        ["cacaniquel"] = "Joga na caça-níquel (alias: slot)",
        ["casino"] = "Mostra os jogos do cassino (alias: cassino)",
        ["pagar"] = "Transferir moedas para outro usuário",
        ["poupanca"] = "Ver seu saldo na poupança",
        ["poupar"] = "Deposita moedas na poupança com juros",
        ["resgatar"] = "Saca moedas da poupança",
        ["trabalhar"] = "Trabalha em um serviço para ganhar moedas",
        ["roubar"] = "Tenta roubar moedas de outro usuário",
        ["historico"] = "Mostra o histórico de transações",
        ["ranking"] = "Ranking global de riqueza",
        ["loja"] = "Mostra os itens disponíveis na loja",
        ["comprar"] = "Compra um item da loja",
        ["usar"] = "Ativa um boost comprado",
        ["inventario"] = "Mostra seus itens",
        ["vender"] = "Revende um item por reembolso parcial",
        ["equipar"] = "Equipa um relógio equipável",
        ["desequipar"] = "Desequipa o relógio ativo",
        ["gif"] = "Buscar, adicionar ou sortear GIFs",
        ["userinfo"] = "Suas informações de usuário",
        ["random"] = "Número aleatório entre min e max",
        ["timer"] = "Temporizador com aviso",
        ["avatar"] = "Avatar de um usuário",
        ["serverinfo"] = "Informações do servidor",
        ["horario"] = "Hora atual (UTC)",
        ["contador"] = "Conta de 1 até N",
        ["reversa"] = "Inverte o texto informado",
        ["welcome"] = "Configura o canal de boas-vindas",
        ["goodbye"] = "Configura o canal de despedidas",
        ["welcomemsg"] = "Define a mensagem de boas-vindas",
        ["goodbyemsg"] = "Define a mensagem de despedida",
        ["welcome off"] = "Desativa as mensagens de boas-vindas",
        ["goodbye off"] = "Desativa as mensagens de despedida",
        ["welcome config"] = "Mostra a configuração de boas-vindas",
        ["voice setup"] = "Define o canal criador de voz",
        ["voice off"] = "Desativa a criação automática de canais de voz",
        ["voice config"] = "Mostra a configuração de canais de voz",
        ["interaction add"] = "Adiciona uma interação do servidor",
        ["interaction remove"] = "Remove uma interação do servidor",
        ["interaction list"] = "Lista as interações do servidor",
        ["prefix"] = "Mostra o prefixo atual do servidor",
        ["prefix set"] = "Define um novo prefixo para o servidor",
        ["prefix reset"] = "Restaura o prefixo padrão",
    };

    public static IReadOnlyList<Category> Categories { get; } = new List<Category>
    {
        new("diversao", "🎮", "Diversão", new[]
        {
            new CommandEntry("/ping · ping", "Responde com Pong!"),
            new CommandEntry("8ball <pergunta>", "Bola 8 mágica responde sua pergunta"),
            new CommandEntry("gorila", "Curiosidade sobre gorilas"),
            new CommandEntry("dado", "Joga um dado de 6 lados"),
            new CommandEntry("flip", "Cara ou coroa"),
        }),
        new("economia", "💰", "Economia", new[]
        {
            new CommandEntry("daily", "Reivindica moedas diárias"),
            new CommandEntry("saldo", "Ver seu saldo (alias: carteira)"),
            new CommandEntry("pagar <usuário> <valor>", "Transferir moedas para outro usuário"),
            new CommandEntry("depositar <valor>", "Move moedas da carteira para o banco"),
            new CommandEntry("sacar <valor>", "Move moedas do banco para a carteira"),
            new CommandEntry("banco", "Consulta seu banco"),
            new CommandEntry("poupanca", "Consulta sua poupança"),
            new CommandEntry("poupar <valor>", "Deposita na poupança com juros diários"),
            new CommandEntry("resgatar <valor>", "Saca moedas da poupança"),
            new CommandEntry("trabalhar [serviço]", "Trabalha em um serviço e ganha moedas"),
            new CommandEntry("roubar <usuário>", "Tenta roubar moedas de outro usuário"),
            new CommandEntry("historico [n]", "Mostra o histórico de transações"),
            new CommandEntry("ranking", "Ranking global de riqueza"),
        }),
        new("jogos", "🃏", "Jogos", new[]
        {
            new CommandEntry("/blackjack <valor>", "Inicia uma mão de Blackjack com botões!"),
            new CommandEntry("Pedir / Parar / Dobrar", "Botões na própria mesa — sem digitar nada"),
            new CommandEntry("hit · stand · double", "Alternativa por prefixo (aliases: pedir, parar, dobrar)"),
        }),
        new("cassino", "🎰", "Cassino", new[]
        {
            new CommandEntry("/roleta <valor>", "Aposta na roleta com botões (número, cor, par/ímpar, metade)"),
            new CommandEntry("roleta <valor> <tipo> <alvo>", "Aposta por prefixo na roleta"),
            new CommandEntry("/cacaniquel <valor>", "Caça-níquel com botões"),
            new CommandEntry("cacaniquel <valor>", "Caça-níquel por prefixo (alias: slot)"),
            new CommandEntry("casino", "Mostra os jogos do cassino (alias: cassino)"),
        }),
        new("loja", "🛒", "Loja", new[]
        {
            new CommandEntry("loja", "Mostra os itens disponíveis na loja"),
            new CommandEntry("comprar <id>", "Compra um item da loja"),
            new CommandEntry("usar <id>", "Ativa um boost comprado"),
            new CommandEntry("inventario", "Mostra seus itens (alias: mochila)"),
            new CommandEntry("vender <id>", "Revende um item por reembolso parcial"),
            new CommandEntry("equipar <id>", "Equipa um relógio (bônus no cassino)"),
            new CommandEntry("desequipar <id>", "Desequipa o relógio ativo"),
            new CommandEntry("loja reload", "Recarrega o catálogo do banco (admin)"),
        }),
        new("utilidade", "🛠️", "Utilidades", new[]
        {
            new CommandEntry("userinfo", "Suas informações de usuário"),
            new CommandEntry("random <min> <max>", "Número aleatório entre min e max"),
            new CommandEntry("timer <segundos>", "Temporizador com aviso"),
            new CommandEntry("avatar [usuário]", "Avatar de um usuário"),
            new CommandEntry("serverinfo", "Informações do servidor"),
            new CommandEntry("horario", "Hora atual (UTC)"),
            new CommandEntry("contador <n>", "Conta de 1 até N"),
            new CommandEntry("reversa <texto>", "Inverte o texto informado"),
        }),
        new("gifs", "🖼️", "GIFs", new[]
        {
            new CommandEntry("gif <nome>", "Envia o GIF salvo com esse nome"),
            new CommandEntry("gif add <nome> <url>", "Salva um GIF no servidor"),
            new CommandEntry("gif random", "Sorteia um GIF salvo"),
        }),
        new("config", "⚙️", "Configuração", new[]
        {
            new CommandEntry("/config status", "Visão geral da configuração deste servidor"),
            new CommandEntry("/config boasvindas-canal|mensagem|desativar|exibir", "Mensagens de boas-vindas por slash"),
            new CommandEntry("/config despedidas-canal|mensagem|desativar", "Mensagens de despedida por slash"),
            new CommandEntry("/config voice-setup|desativar|remover|exibir", "Canais de voz sob demanda por slash"),
            new CommandEntry("/config prefixo-definir|resetar|exibir", "Prefixo do bot por slash"),
            new CommandEntry("welcome <canal> · welcomemsg <texto> · welcome off/config", "Mensagens de boas-vindas (prefixo)"),
            new CommandEntry("goodbye <canal> · goodbyemsg <texto> · goodbye off/config", "Mensagens de despedida (prefixo)"),
            new CommandEntry("voice setup <canal> · voice off/config", "Canais de voz sob demanda (prefixo)"),
            new CommandEntry("prefix [set|reset]", "Prefixo do bot neste servidor (prefixo)"),
            new CommandEntry("interaction add/remove/list", "Respostas automáticas personalizadas"),
        }),
    };

    public static Embed BuildOverviewEmbed(IUser botUser)
    {
        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{botUser.Username} — Comandos", botUser.GetAvatarUrl())
            .WithTitle("Central de Ajuda")
            .WithDescription(
                "Selecione uma categoria no menu abaixo.\n\n" +
                "✨ **Já disponíveis por `/`:** `blackjack`, `roleta`, `cacaniquel`, `config`, `ajuda`\n" +
                "Os demais comandos usem com o prefixo do servidor (padrão: `macaco`).");

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
            sb.AppendLine($"`{cmd.Name}` — {cmd.Description}");

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
