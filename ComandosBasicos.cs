using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using GorillazDiscordBot.Service;

namespace GorillazDiscordBot
{
    public class ComandosBasicos : ModuleBase<SocketCommandContext>
    {
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
            sb.AppendLine("**!foto** — Comando para tirar foto do usuário (ainda não implementado).");
            sb.AppendLine("**!azul** — Mostra imagens aleatórias com frases divertidas.");
            sb.AppendLine("**!joia** — Envia um GIF animado.");
            sb.AppendLine("**!malicia** — Envia outro GIF animado engraçado.");
            sb.AppendLine("**!galado** — Envia um GIF de raposa animada.");
            sb.AppendLine("**!fazol** — Envia GIFs variados aleatórios.");
            sb.AppendLine("**!senta** — Envia GIF e frase para 'sentar aqui'.");
            sb.AppendLine("**!cotacao** — Mostra a cotação atual de várias moedas.");

            await ReplyAsync(sb.ToString());
        }

        [Command("ping")]
        [Summary("Responde com Pong!")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

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
        [Summary("Tirar a foto do usuario")]
        public async Task FotoAsync()
        {
            await ReplyAsync("Ainda não implementado");
        }

        [Command("azul")]
        [Summary("Mostrar as bolas")]
        public async Task MostrarBolasAsync()
        {
            int opcao = new Random().Next(1, 3);

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
        public async Task JoiaAsync()
        {
            await ReplyAsync("https://c.tenor.com/_1qbDhMf0ZQAAAAd/tenor.gif");
        }

        [Command("malicia")]
        public async Task MaliciaAsync()
        {
            await ReplyAsync("https://tenor.com/pt-BR/view/yzxh-funny-gif-15066639939174565798");
        }

        [Command("galado")]
        public async Task GaladoAsync()
        {
            await ReplyAsync("https://tenor.com/pt-BR/view/fox-cartoon-milk-mouth-face-gif-13048116");
        }

        [Command("fazol")]
        public async Task FazOLAsync()
        {
            int opcao = new Random().Next(1, 3);

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
    }
}
