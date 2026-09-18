using Discord.Commands;

namespace GorillazDiscordBot.Commands;

[Group("gif")]
public class GifManageModule : ModuleBase<SocketCommandContext>
{
    [Command]
    public async Task GifRouterAsync([Remainder] string input)
        => await ReplyAsync("❌ O comando `gif` passou a fazer parte das interações. Use `/interacao criar` (tipo: GIF, áudio ou vídeo) para salvar mídia e dispará-la por trigger no chat.");
}