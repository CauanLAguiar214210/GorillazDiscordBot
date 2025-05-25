using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using GorillazDiscordBot.Service;

namespace GorillazDiscordBot
{
    public class ComandosBasicos : ModuleBase<SocketCommandContext>
    {
        private static readonly Random _random = new Random();

        [Command("ajuda")]
        [Summary("Mostra os comandos disponíveis com descrição.")]
        public async Task AjudaAsync()
        {
            var sb = new StringBuilder();

            sb.AppendLine("🛠️ **Comandos disponíveis:**");
            sb.AppendLine();
            sb.AppendLine("**!ping** — Responde com Pong!");
            sb.AppendLine("**!ajuda** — Mostra esta mensagem de ajuda.");
            sb.AppendLine("**!mama** — Responde com uma sequência de 'Glub!' e termina com 'Engasguei!'.");
            sb.AppendLine("**!banana** — Responde com 'Cadê?!' repetido e finaliza com 'Bananinha gostosa?!'.");
            sb.AppendLine("**!azul** — Mostra imagens aleatórias com frases divertidas.");
            sb.AppendLine("**!joia** — Envia um GIF animado.");
            sb.AppendLine("**!malicia** — Envia outro GIF animado engraçado.");
            sb.AppendLine("**!galado** — Envia um GIF de raposa animada.");
            sb.AppendLine("**!fazol** — Envia GIFs variados aleatórios.");
            sb.AppendLine("**!senta** — Envia GIF e frase para 'sentar aqui'.");
            sb.AppendLine("**!cotacao** — Mostra a cotação atual de várias moedas.");
            sb.AppendLine();
            sb.AppendLine("**!dado** — Joga um dado de 6 lados e mostra o resultado.");
            sb.AppendLine("**!flip** — Joga cara ou coroa.");
            sb.AppendLine("**!8ball [pergunta]** — Responde sua pergunta com a Bola 8 mágica.");
            sb.AppendLine("**!userinfo** — Exibe informações básicas sobre você.");
            sb.AppendLine("**!tempo [cidade]** — Mostra a previsão do tempo para uma cidade (ainda não implementado).");
            sb.AppendLine("**!random [min] [max]** — Gera um número aleatório entre dois valores.");
            sb.AppendLine("**!gorila** — Envia uma curiosidade sobre gorilas.");
            sb.AppendLine("**!timer [segundos]** — Define um temporizador em segundos e avisa quando terminar.");
            sb.AppendLine("**!avatar [usuário]** — Mostra o avatar do usuário mencionado ou seu próprio avatar.");
            sb.AppendLine("**!serverinfo** — Mostra informações básicas do servidor.");
            sb.AppendLine("**!horario** — Mostra a hora atual do servidor (UTC).");
            sb.AppendLine("**!contador [número]** — Conta de 1 até o número informado (máximo 20).");
            sb.AppendLine("**!reversa [texto]** — Retorna a mensagem invertida.");

            await ReplyAsync(sb.ToString());
        }

        [Command("ping")]
        public async Task PingAsync() => await ReplyAsync("Pong!");

        [Command("mama")]
        [Summary("Responde com Mamando!")]
        public async Task MamaAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                await ReplyAsync("Glub!");
            }
            await ReplyAsync("Engasguei!");
        }

        [Command("banana")]
        [Summary("Responde com Bananinha!")]
        public async Task BananaAsync()
        {
            for (int i = 0; i < 3; i++)
            {
                await ReplyAsync("Cadê?!");
            }
            await ReplyAsync("Bananinha gostosa?!");
        }

        [Command("foto")]
        public async Task FotoAsync() => await ReplyAsync("Ainda não implementado");        

        [Command("azul")]
        [Summary("Mostrar as bolas")]
        public async Task MostrarBolasAsync()
        {
            int opcao = _random.Next(1, 4);

            switch (opcao)
            {
                case 1:
                    await ReplyAsync("https://media.istockphoto.com/photos/vervet-monkey-tarangire-national-park-tanzania-picture-id927038986?k=20&m=927038986&s=612x612&w=0&h=u2565lP4wg1fTIJABhI-0gMj-RCdK3y98k9honcDtZ0=");
                    await ReplyAsync("É que vc é moh gostosa");
                    break;
                case 2:
                    await ReplyAsync("https://oeco.org.br/wp-content/uploads/oeco-migration/images/stories/mai2013/221212_Tarangire_2017.jpg?is-pending-load=1");
                    await ReplyAsync("Cortei o cabelo! Gostaram?");
                    break;
                case 3:
                    await ReplyAsync("https://external-preview.redd.it/eR6q_NZUqtFxJTrRpZeawH_XJi9NwRQ_yRdkrE3h7oM.jpg?width=640&crop=smart&auto=webp&s=7f49eda266edda7715575b23893df24e3bfdbc18");
                    await ReplyAsync("So timido");
                    break;
            }
        }

        [Command("joia")]
        public async Task JoiaAsync() => await ReplyAsync("https://c.tenor.com/_1qbDhMf0ZQAAAAd/tenor.gif");        

        [Command("malicia")]
        public async Task MaliciaAsync() => await ReplyAsync("https://tenor.com/pt-BR/view/yzxh-funny-gif-15066639939174565798");        

        [Command("galado")]
        public async Task GaladoAsync() => await ReplyAsync("https://tenor.com/pt-BR/view/fox-cartoon-milk-mouth-face-gif-13048116");        

        [Command("fazol")]
        public async Task FazOLAsync()
        {
            int opcao = _random.Next(1, 4);

            switch (opcao)
            {
                case 1:
                    await ReplyAsync("https://tenor.com/pt-BR/view/fcblondedgif-gif-6473137128583678578");
                    break;
                case 2:
                    await ReplyAsync("https://tenor.com/pt-BR/view/glee-sue-sylvester-loser-take-the-l-you-suck-gif-21774438");
                    break;
                case 3:
                    await ReplyAsync("https://tenor.com/pt-BR/view/l-faz-o-l-faz-lula-faça-o-l-gif-1320536772423024769");
                    break;
            }
        }

        [Command("senta")]
        public async Task SentaAquiAsync()
        {
            await ReplyAsync("https://tenor.com/pt-BR/view/monkey-sofa-gif-17205851679107178327");
            await ReplyAsync("Senta aqui!! Eu mandei vc sentar!");
        }

        [Command("f1")]
        public async Task ObterClassificacaoPilotosAsync()
        {
            var f1Service = new FOneService();

            var pilotos = await f1Service.ObterClassificacaoPilotosAsync();

            if (pilotos != null)
            {
                foreach (var p in pilotos)
                {
                    await ReplyAsync($"{p.Position} - {p.Driver.GivenName} {p.Driver.FamilyName} - {p.Points} pontos");
                }
            }
            else
            {
                await ReplyAsync("Não foi possível obter a classificação.");
            }
        }

        [Command("Cotacao")]
        [Summary("Mostra a cotação atual de várias moedas.")]
        public async Task CotacaoAsync()
        {
            var moedas = new string[] { "USD-BRL", "EUR-BRL", "GBP-BRL", "ARS-BRL" };

            var nomesCompletos = new Dictionary<string, string>()
            {
                { "USD-BRL", "Dólar Americano" },
                { "EUR-BRL", "Euro" },
                { "GBP-BRL", "Libra Esterlina" },
                { "ARS-BRL", "Peso Argentino" }
            };

            var cotacaoService = new CotacaoService();
            var cotacoes = await cotacaoService.ObterCotacoesAsync(moedas);

            var sb = new StringBuilder();
            sb.AppendLine("💰 **Cotação das Moedas:**");

            foreach (var par in cotacoes)
            {
                var nome = nomesCompletos.ContainsKey(par.Key) ? nomesCompletos[par.Key] : par.Key;

                if (par.Value.HasValue)
                    sb.AppendLine($"{nome}: R$ {par.Value.Value:F2}");
                else
                    sb.AppendLine($"{nome}: cotação indisponível");
            }

            await ReplyAsync(sb.ToString());
        }

        [Command("dado")]
        [Summary("Joga um dado de 6 lados e mostra o resultado.")]
        public async Task DiceAsync()
        {
            int resultado = _random.Next(1, 7);
            await ReplyAsync($"🎲 Você rolou: **{resultado}**");
        }

        [Command("flip")]
        [Summary("Joga cara ou coroa.")]
        public async Task FlipAsync()
        {
            var resultado = _random.Next(0, 2) == 0 ? "Cara" : "Coroa";
            await ReplyAsync($"🪙 O resultado foi: **{resultado}**");
        }

        [Command("8ball")]
        [Summary("Responde sua pergunta com a Bola 8 mágica.")]
        public async Task EightBallAsync([Remainder][Summary("Pergunta para a Bola 8")] string pergunta)
        {
            string[] respostas = new[]
            {
                "Com certeza!",
                "Não conte com isso.",
                "Talvez, quem sabe?",
                "Sem dúvida.",
                "Pergunte novamente mais tarde.",
                "Minha resposta é não.",
                "É provável.",
                "Não posso prever agora."
            };

            int idx = _random.Next(respostas.Length);
            await ReplyAsync($"🎱 Pergunta: {pergunta}\nResposta: **{respostas[idx]}**");
        }

        [Command("userinfo")]
        [Summary("Exibe informações básicas sobre você.")]
        public async Task UserInfoAsync()
        {
            var user = Context.User;
            string info = $"👤 Usuário: {user.Username}#{user.Discriminator}\n" +
                          $"ID: {user.Id}\n" +
                          $"Criado em: {user.CreatedAt.DateTime.ToString("dd/MM/yyyy")}\n";

            await ReplyAsync(info);
        }

        [Command("tempo")]
        [Summary("Mostra a previsão do tempo para uma cidade.")]
        public async Task TempoAsync([Remainder] string cidade)
        {
            // Exemplo simples sem implementação real
            await ReplyAsync($"🌤️ A previsão do tempo para {cidade} ainda não está implementada, mas já já chega!");
            // Aqui você pode integrar uma API de clima tipo OpenWeatherMap para dados reais.
        }

        [Command("random")]
        [Summary("Gera um número aleatório entre dois valores.")]
        public async Task RandomAsync(int min, int max)
        {
            if (min > max)
            {
                await ReplyAsync("O valor mínimo deve ser menor ou igual ao máximo.");
                return;
            }
            int numero = _random.Next(min, max + 1);
            await ReplyAsync($"🎲 Número aleatório entre {min} e {max}: **{numero}**");
        }

        [Command("gorila")]
        [Summary("Envia uma curiosidade sobre gorilas.")]
        public async Task GorilaAsync()
        {
            var curiosidades = new[]
            {
                "Gorilas podem rir, chorar e até usar ferramentas!",
                "Eles vivem em grupos liderados por um macho dominante chamado 'prata-costas'.",
                "Gorilas são 98% geneticamente parecidos com humanos.",
                "Eles são herbívoros e podem comer até 40 libras de comida por dia."
            };
            int idx = _random.Next(curiosidades.Length);
            await ReplyAsync($"🦍 Curiosidade: {curiosidades[idx]}");
        }

        [Command("timer")]
        [Summary("Define um temporizador em segundos e avisa quando terminar.")]
        public async Task TimerAsync(int segundos)
        {
            if (segundos <= 0)
            {
                await ReplyAsync("⏳ Por favor, informe um tempo válido em segundos.");
                return;
            }

            await ReplyAsync($"⏳ Temporizador iniciado: {segundos} segundos.");

            await Task.Delay(segundos * 1000);

            await ReplyAsync($"{Context.User.Mention} ⏰ Tempo esgotado!");
        }

        [Command("avatar")]
        [Summary("Mostra o avatar do usuário mencionado ou seu próprio avatar.")]
        public async Task AvatarAsync(Discord.IUser user = null)
        {
            user ??= Context.User;

            var avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
            await ReplyAsync($"{user.Username}'s avatar: {avatarUrl}");
        }

        [Command("serverinfo")]
        [Summary("Mostra informações básicas do servidor.")]
        public async Task ServerInfoAsync()
        {
            var guild = Context.Guild;
            if (guild == null)
            {
                await ReplyAsync("Este comando só pode ser usado dentro de um servidor.");
                return;
            }

            var info = new StringBuilder();
            info.AppendLine($"🏰 Nome do servidor: {guild.Name}");
            info.AppendLine($"👥 Membros: {guild.MemberCount}");
            info.AppendLine($"🌐 Região: {guild.VoiceRegionId}");
            info.AppendLine($"📅 Criado em: {guild.CreatedAt.DateTime.ToString("dd/MM/yyyy")}");
            info.AppendLine($"🛡️ Dono: {guild.Owner.Username}#{guild.Owner.Discriminator}");

            await ReplyAsync(info.ToString());
        }

        [Command("horario")]
        [Summary("Mostra a hora atual do servidor.")]
        public async Task HorarioAsync()
        {
            var horaAtual = DateTime.UtcNow; // UTC para padrão, pode ajustar para outro fuso
            await ReplyAsync($"⏰ Hora atual (UTC): {horaAtual:HH:mm:ss}");
        }

        [Command("contador")]
        [Summary("Conta de 1 até o número informado (máximo 20).")]
        public async Task ContadorAsync(int max)
        {
            if (max < 1 || max > 20)
            {
                await ReplyAsync("Por favor, escolha um número entre 1 e 20.");
                return;
            }

            var sb = new StringBuilder();
            for (int i = 1; i <= max; i++)
            {
                sb.Append(i).Append(' ');
            }

            await ReplyAsync(sb.ToString());
        }

        [Command("reversa")]
        [Summary("Retorna a mensagem invertida.")]
        public async Task ReversaAsync([Remainder] string texto)
        {
            var arr = texto.ToCharArray();
            Array.Reverse(arr);
            string textoInvertido = new string(arr);
            await ReplyAsync(textoInvertido);
        }
    }
}
