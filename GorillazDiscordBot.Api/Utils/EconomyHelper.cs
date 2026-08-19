using Discord.Commands;

namespace GorillazDiscordBot.Utils;

public static class EconomyHelper
{
    public static bool TryParsePositiveAmount(string input, out int amount, out string? error)
    {
        amount = 0;
        error = null;

        if (!int.TryParse(input, out amount))
        {
            error = "⚠️ Informe um valor numérico. Exemplo: `bet 100`";
            return false;
        }

        if (amount <= 0)
        {
            error = "⚠️ O valor deve ser positivo.";
            return false;
        }

        return true;
    }

    public static async Task<bool> TryParseAndReplyAsync(string input, ICommandContext context)
    {
        if (!TryParsePositiveAmount(input, out _, out var error))
        {
            await context.Channel.SendMessageAsync(error!);
            return false;
        }
        return true;
    }
}
