namespace GorillazDiscordBot.Domain.Entity.Profile;

public sealed record LicenseInfo(LicenseDomain Domain, string Name, string Emoji, SchoolingLevel MinSchooling);

public static class LicenseProgression
{
    public static readonly IReadOnlyList<LicenseLevel> All = new[]
    {
        LicenseLevel.A,
        LicenseLevel.B,
        LicenseLevel.C,
        LicenseLevel.D,
        LicenseLevel.E,
        LicenseLevel.Arrais,
        LicenseLevel.Mestre,
        LicenseLevel.Capitao,
        LicenseLevel.PilotoPrivado,
        LicenseLevel.PilotoComercial,
        LicenseLevel.PilotoLinhaAerea
    };

    public static readonly IReadOnlyList<LicenseDomain> Domains = new[]
    {
        LicenseDomain.Terrestre,
        LicenseDomain.Maritima,
        LicenseDomain.Aerea
    };

    public static LicenseInfo Info(LicenseLevel level) => level switch
    {
        LicenseLevel.A => new LicenseInfo(LicenseDomain.Terrestre, "Categoria A — Moto", "🏍️", SchoolingLevel.EnsinoFundamental1),
        LicenseLevel.B => new LicenseInfo(LicenseDomain.Terrestre, "Categoria B — Carro", "🚗", SchoolingLevel.EnsinoFundamental2),
        LicenseLevel.C => new LicenseInfo(LicenseDomain.Terrestre, "Categoria C — Caminhão", "🚚", SchoolingLevel.EnsinoMedio),
        LicenseLevel.D => new LicenseInfo(LicenseDomain.Terrestre, "Categoria D — Ônibus", "🚌", SchoolingLevel.EnsinoMedio),
        LicenseLevel.E => new LicenseInfo(LicenseDomain.Terrestre, "Categoria E — Carreta", "🚛", SchoolingLevel.EnsinoMedio),
        LicenseLevel.Arrais => new LicenseInfo(LicenseDomain.Maritima, "Arrais-Amador", "🚤", SchoolingLevel.EnsinoMedio),
        LicenseLevel.Mestre => new LicenseInfo(LicenseDomain.Maritima, "Mestre-Amador", "⛵", SchoolingLevel.EnsinoMedio),
        LicenseLevel.Capitao => new LicenseInfo(LicenseDomain.Maritima, "Capitão-Amador", "🛳️", SchoolingLevel.EnsinoSuperior),
        LicenseLevel.PilotoPrivado => new LicenseInfo(LicenseDomain.Aerea, "Piloto Privado", "✈️", SchoolingLevel.EnsinoSuperior),
        LicenseLevel.PilotoComercial => new LicenseInfo(LicenseDomain.Aerea, "Piloto Comercial", "🛫", SchoolingLevel.EnsinoSuperior),
        LicenseLevel.PilotoLinhaAerea => new LicenseInfo(LicenseDomain.Aerea, "Piloto de Linha Aérea", "🛩️", SchoolingLevel.EnsinoSuperior),
        _ => new LicenseInfo(LicenseDomain.Terrestre, "Desconhecida", "❓", SchoolingLevel.Nenhuma)
    };

    public static IReadOnlyList<LicenseLevel> ByDomain(LicenseDomain domain)
        => All.Where(l => Info(l).Domain == domain).ToList();

    public static IReadOnlyList<LicenseLevel> Sequence(LicenseDomain domain)
        => ByDomain(domain);

    public static int PositionInSequence(LicenseLevel level)
    {
        var sequence = ByDomain(Info(level).Domain);
        for (var i = 0; i < sequence.Count; i++)
        {
            if (sequence[i] == level)
                return i;
        }
        return -1;
    }

    public static SchoolingLevel RequiredSchooling(LicenseLevel level)
        => Info(level).MinSchooling;

    public static LicenseLevel? Previous(LicenseLevel level)
    {
        var index = PositionInSequence(level);
        return index > 0 ? Sequence(Info(level).Domain)[index - 1] : null;
    }

    public static LicenseLevel? Next(LicenseLevel level)
    {
        var sequence = Sequence(Info(level).Domain);
        var index = PositionInSequence(level);
        return index >= 0 && index + 1 < sequence.Count ? sequence[index + 1] : null;
    }

    public static bool IsMax(LicenseLevel level)
    {
        var sequence = Sequence(Info(level).Domain);
        return sequence.Count > 0 && level == sequence[^1];
    }

    public static LicenseLevel? Prerequisite(LicenseLevel level) => Previous(level);

    public static bool MeetsPrerequisite(LicenseLevel level, IReadOnlyCollection<LicenseLevel> owned)
    {
        var prereq = Prerequisite(level);
        return prereq is null || owned.Contains(prereq.Value);
    }

    public static LicenseDomain DomainOf(LicenseLevel level)
        => Info(level).Domain;
}

public static class LicenseCosts
{
    public const ulong TerrestreCost = 2000;
    public const ulong MaritimaCost = 25000;
    public const ulong AereaCost = 35000;

    public static ulong ExamCost(LicenseLevel level) => LicenseProgression.DomainOf(level) switch
    {
        LicenseDomain.Terrestre => TerrestreCost,
        LicenseDomain.Maritima => MaritimaCost,
        LicenseDomain.Aerea => AereaCost,
        _ => 0
    };
}

public static class LicenseQuizzes
{
    public const int QuestionsPerExam = 3;
    public const int PassingScore = 2;

    private static readonly IReadOnlyDictionary<LicenseLevel, IReadOnlyList<MathQuestion>> Bank =
        new Dictionary<LicenseLevel, IReadOnlyList<MathQuestion>>
        {
            [LicenseLevel.A] = new[]
            {
                Q("O que é obrigatório para o condutor de moto?", 2, "Cinto de segurança", "Extintor", "Capacete", "Farol alto"),
                Q("Quando a seta deve ser usada?", 3, "Só em ultrapassagem", "Nunca", "Só em cruzamento", "Antes de mudar de faixa ou direção"),
                Q("Sinal vermelho significa:", 0, "Parar", "Acelerar", "Seguir com cuidado", "Buzinar"),
                Q("O que é o corredor?", 1, "Faixa exclusiva de ônibus", "Espaço entre duas faixas de veículos", "Área de pedestres", "Rua sem sinalização"),
                Q("Ultrapassar pela direita é:", 0, "Proibido", "Permitido", "Permitido só em rodovia", "Obrigatório"),
                Q("O que é direção defensiva?", 1, "Dirigir em alta velocidade", "Dirigir prevenindo acidentes", "Dirigir apenas com GPS", "Dirigir à noite")
            },
            [LicenseLevel.B] = new[]
            {
                Q("Para que serve o cinto de segurança?", 1, "Para não receber multa", "Proteger os ocupantes em colisão", "Ajustar o banco", "Sinalizar parada"),
                Q("Sinal amarelo significa:", 2, "Parar obrigatoriamente", "Passar livre", "Atenção: parar se possível", "Buzinar"),
                Q("O que é aquaplanagem?", 1, "Lavar o carro", "Perda de aderência dos pneus na água", "Pneus novos", "Frenagem em pista seca"),
                Q("Dirigir após beber é:", 0, "Proibido", "Permitido em baixa dose", "Permitido à noite", "Permitido apenas em rodovia"),
                Q("Placa com a palavra 'PARE' indica:", 0, "Parada obrigatória", "Atenção opcional", "Sentido único", "Área escolar"),
                Q("Ao ver um pedestre atravessando a faixa, você deve:", 3, "Buzinar", "Passar devagar sem parar", "Contornar", "Parar e deixá-lo atravessar")
            },
            [LicenseLevel.C] = new[]
            {
                Q("Antes de trafegar com caminhão, o que deve estar normatizado?", 2, "Só o combustível", "Apenas a cabine", "Peso e amarração da carga", "A caixa de ferramentas"),
                Q("O que é o tacógrafo?", 3, "Ferramenta de eixo", "Peça do motor", "Contador de pneus", "Registrador da jornada de direção"),
                Q("Em descidas longas, o caminhoneiro deve:", 0, "Usar o freio motor", "Desligar o motor", "Puxar o freio de mão", "Reduzir para 1ª marcha"),
                Q("Documento obrigatório do veículo:", 1, "Nota fiscal", "CRLV", "Carteira de trabalho", "Recibo de seguro"),
                Q("O que a balança de pesagem fiscaliza?", 1, "Velocidade", "Peso bruto do veículo", "Altura da carga", "Pressão dos pneus"),
                Q("Ao estacionar com inclinação, além do freio de mão deve-se:", 3, "Buzinar", "Acelerar", "Desligar o farol", "Engrenar uma marcha")
            },
            [LicenseLevel.D] = new[]
            {
                Q("Antes de fechar as portas do ônibus, o motorista deve:", 1, "Acelerar", "Conferir os espelhos", "Apagar a luz", "Buzinar"),
                Q("Em via com muitos pedestres, o correto é:", 1, "Manter a velocidade", "Buzinar", "Reduzir a velocidade", "Desviar"),
                Q("O que é o ponto cego do ônibus?", 1, "Farol queimado", "Área que o motorista não vê pelos espelhos", "Banco traseiro", "Porta traseira"),
                Q("Transporte de passageiros em pé é:", 2, "Sempre proibido", "Obrigatório", "Permitido conforme regulamento", "Permitido só à noite"),
                Q("Ao fazer curva com ônibus, o motorista deve:", 3, "Acelerar na curva", "Ignorar a sinalização", "Usar a faixa de pedestre", "Abrir a curva e reduzir"),
                Q("Documentação exigida para transporte de passageiros:", 0, "Licença de transporte e registro do veículo", "Certificado de só torneio", "Somente a carteira de motorista", "Nenhuma")
            },
            [LicenseLevel.E] = new[]
            {
                Q("O que é uma combinação de veículos?", 0, "Cavalo mecânico com reboque ou semirreboque", "Dois carros de passeio", "Moto com carro", "Ônibus articulado"),
                Q("O que é o 'rabo de cobra'?", 1, "Pneus velhos", "Oscilação lateral do reboque", "Carga solta", "Barulho do motor"),
                Q("Ao dar ré com carreta, o ideal é contar com:", 2, "Só o espelho", "Velocidade alta", "Auxílio de terceiros quando possível", "Farol alto"),
                Q("Antes de trafegar, o engate do reboque deve estar:", 1, "Solto", "Travado e conferido", "Lubrificado com graxa", "Sem inspeção"),
                Q("Em curvas fechadas com carreta, o motorista deve:", 3, "Cortar a curva", "Acelerar", "Parar no meio", "Entrelargar a curva"),
                Q("Para quais cargas é obrigatória a devida amarração?", 2, "Qualquer carga solta", "Só cargas vivas", "Todas as cargas", "Nenhuma")
            },
            [LicenseLevel.Arrais] = new[]
            {
                Q("O que é obrigatório a bordo de uma embarcação?", 1, "GPS", "Coletes salva-vidas para todos", "Bússola digital", "Rádio FM"),
                Q("Antes de zarpar, o importante é verificar:", 0, "Condições do tempo e do mar", "A cor do barco", "A música da rádio", "O número de turistas"),
                Q("O que significa 'estibordo'?", 2, "Proa da embarcação", "Popa da embarcação", "Lado direito da embarcação", "Lado esquerdo da embarcação"),
                Q("O que significa 'bombordo'?", 3, "Proa da embarcação", "Lado direito da embarcação", "Popa da embarcação", "Lado esquerdo da embarcação"),
                Q("Ao avistar mau tempo se aproximando, você deve:", 0, "Retornar ao porto seguro", "Acelerar para atravessar", "Ignorar", "Desligar as luzes"),
                Q("Qual destes é um item de segurança obrigatório?", 2, "Sombrero", "Churrasqueira", "Extintor de incêndio", "Televisão")
            },
            [LicenseLevel.Mestre] = new[]
            {
                Q("Uma embarcação maior exige do condutor:", 1, "Menos atenção", "Planejamento de navegação", "Nadar mais", "Velocidade máxima"),
                Q("O que é navegação de cabotagem?", 0, "Navegar próximo à costa", "Atravessar o oceano", "Navegar em rios", "Navegar à vela"),
                Q("O que é uma carta náutica?", 2, "Documento de porte", "Pedido de tripulação", "Mapa de navegação aquaviária", "Certificado de seguro"),
                Q("As luzes de navegação servem para:", 1, "Iluminar a cabine", "Sinalizar posição e rumo à noite", "Carregar a bateria", "Assustar peixes"),
                Q("Antes de atracar, o condutor deve conferir:", 3, "A comida", "O combustível da caldeira", "As janelas", "Cabos, defensas e máquina"),
                Q("Encontro com outra embarcação em rumo de colisão: você deve", 0, "Manter-se afastado conforme as regras", "Buzinar repetidamente", "Acelerar", "Apagar as luzes")
            },
            [LicenseLevel.Capitao] = new[]
            {
                Q("O que é o calado de uma embarcação?", 1, "O comprimento do casco", "A profundidade que o casco mergulha", "O peso da carga", "A distância até o porto"),
                Q("Balizamento: uma boia verde deve ficar à...", 1, "Bombordo", "Boreste/estibordo", "Proa", "Popa"),
                Q("O que é um arco de corrente?", 2, "Tipo de âncora", "Jogo de luzes", "Rede de caça-pesca", "Grande embarcação pesqueira"),
                Q("Cargas perigosas a bordo exigem:", 0, "Sinalização e procedimentos específicos", "Somente cobertura", "Passageiros extras", "Nenhum cuidado"),
                Q("Em fonética de rádio, 'Mayday' é:", 3, "Um pedido de tempo", "Uma hora marcada", "Um tipo de âncora", "Sinal de perigo extremo"),
                Q("Comandante embarcado é responsável por:", 1, "Só pelo motor", "Toda a segurança da tripulação e navegação", "Somente a cobrança", "Nenhuma responsabilidade")
            },
            [LicenseLevel.PilotoPrivado] = new[]
            {
                Q("Qual documento comprova a habilitação do piloto?", 2, "Passaporte", "Carteira de identidade", "Licença de piloto", "Comprovante de residência"),
                Q("Antes do voo, o piloto deve verificar:", 1, "Apenas o combustível", "Condições meteorológicas e plano de voo", "Só o rádio", "A limpeza"),
                Q("O que é o plano de voo?", 0, "Rota e dados do voo notificados à autoridade", "Lista de passageiros", "Checklist do avião", "Cardápio de bordo"),
                Q("Decolagem em direção ao vento favorece:", 1, "Menos segurança", "Mais sustentação na decolagem", "Maior consumo", "Turbulência"),
                Q("Qual destes é um instrumento essencial de voo?", 3, "Som", "Rádio AM", "GPS", "Altímetro"),
                Q("Visibilidade baixa e teto baixo indicam:", 1, "Ótimo dia para voar", "Condições desfavoráveis ao voo", "Voo mais rápido", "Meno consumo")
            },
            [LicenseLevel.PilotoComercial] = new[]
            {
                Q("Quem autoriza a operação de voos comerciais?", 1, "O piloto", "A autoridade de aviação civil", "O aeroporto", "A companhia aérea"),
                Q("Antes do voo comercial, o comandante deve:", 0, "Confirmar carga, combustível e briefing", "Apenas assinar o rádio", "Escolher a música", "Apagar as luzes"),
                Q("O que é a torre de controle?", 2, "Hotel de pilotos", "Museu de aviação", "Órgão que coordena pousos e decolagens", "Posto de combustível"),
                Q("Um pouso com vento de través exige:", 1, "Maior velocidade", "Correção adicional do piloto", "Desligar o rádio", "Mais passageiros"),
                Q("Em emergência, o primeiro procedimento do piloto é:", 0, "Manter o controle da aeronave", "Saltar", "Apagar tudo", "Chamar telefone"),
                Q("O que é o check-in do voo?" , 2, "Obrigação do co-piloto", "Apoio de terra", "Conferência dos itens antes da decolagem", "Impressão de bilhetes")
            },
            [LicenseLevel.PilotoLinhaAerea] = new[]
            {
                Q("Quem tem autoridade final a bordo de uma aeronave?", 3, "O comissário", "A empresa", "O despachante", "O comandante/piloto em comando"),
                Q("O que é a tripulação de cabine?", 1, "Só o capitão", "Pilotos que comandam o voo", "Comissários e pessoal de bordo", "Funcionários do hangar"),
                Q("Voos de longo curso exigem:", 0, "Planejamento de combustível e tripulação", "Apenas café", "Menos combustível", "Pista curta"),
                Q("Padrões instrumentais em teto baixo exigem:", 2, "Voo visual", "Parar de voar", "Procedimentos de aproximação por instrumentos", "Abrir a janela"),
                Q("O briefing de segurança antes do voo é feito para:", 1, "Passageiros escolherem lugar", "Informar procedimentos e emergências", "Entreter o voo", "Testar a comida"),
                Q("Em falha de motor em cruzeiro, o piloto deve:", 3, "Escolher passageiro para opinar", "Desligar tudo", "Acelerar", "Seguir o procedimento de emergência da aeronave")
            }
        };

    public static IReadOnlyList<MathQuestion> For(LicenseLevel level)
        => Bank.TryGetValue(level, out var questions) ? questions : Array.Empty<MathQuestion>();

    private static MathQuestion Q(string text, int correctIndex, params string[] options)
        => new(text, options, correctIndex);
}