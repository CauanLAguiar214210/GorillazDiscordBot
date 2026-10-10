using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Config;

public partial class ConfigSlashModule
{
    [Group("modulo", "Consulta os módulos deste servidor")]
    public sealed class ModuleStatusModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly ModuleAccessService _moduleAccess;

        public ModuleStatusModule(ModuleAccessService moduleAccess)
        {
            _moduleAccess = moduleAccess;
        }

        [SlashCommand("status", "Mostra os módulos liberados neste servidor")]
        public async Task StatusAsync()
        {
            if (!await CommandGuards.GuardAdminInteractionAsync(Context))
                return;

            var statuses = await _moduleAccess.GetStatusesAsync(Context.Guild.Id);
            var description = string.Join("\n", statuses.Select(status =>
                $"{(status.Enabled ? "✅" : "⛔")} **{status.Name}**"));

            var embed = new EmbedBuilder()
                .WithTitle("🔧 Módulos do servidor")
                .WithDescription(description)
                .WithColor(Color.DarkGrey)
                .WithFooter("A ativação dos módulos é gerenciada pelo administrador do serviço.")
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);
        }
    }
}
