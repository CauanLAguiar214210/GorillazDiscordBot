using Discord.Commands;

namespace GorillazDiscordBot.Commands;

public class GifModule : ModuleBase<SocketCommandContext>
{
    private static readonly Random _random = new();

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

    [Command("azul")]
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

    [Command("foto")]
    public async Task FotoAsync() => await ReplyAsync("Ainda não implementado");

    [Command("tamanhodopinto")]
    public async Task TamanhoDoPintoAsync()
    {
        int tamanhoDoPinto = _random.Next(0, 50);
        int chancePirocaAleatoria = _random.Next(1, 50);
        string pinto = "8";

        for (int i = 0; i < tamanhoDoPinto; i++)
            pinto += "=";

        pinto += "D";

        await ReplyAsync("Seu pinto tem o tamanho de: " + pinto + " (" + tamanhoDoPinto + " cm)" + " " + chancePirocaAleatoria.ToString());

        string? gifUrl = chancePirocaAleatoria switch
        {
            5  => "https://26.media.tumblr.com/tumblr_lvr18jYoTH1qjk5ivo1_400.gif",
            25 => "https://hugeblackman.com/wp-content/uploads/2015/11/tumblr_msc5hiWof01s82hilo1_400.gif",
            50 => "https://18gayteen.com/wp-content/uploads/2016/02/big_black_cock_10.gif",
            13 => "https://imagex1.sx.cdn.live/images/pinporn/2017/02/18/17374585.gif?width=460",
            _  => null
        };

        if (gifUrl != null)
            await ReplyAsync(gifUrl);
    }
}
