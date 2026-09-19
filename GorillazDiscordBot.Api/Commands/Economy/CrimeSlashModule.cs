using System.Text;
using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Economy;

[Group("crime", "Ações do submundo: furtos, assaltos, arsenal e fiança")]
public class CrimeSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;
    private readonly ShopService _shop;
    private readonly ICharacterProfileRepository _profiles;

    public CrimeSlashModule(
        IEconomyRepository economy,
        IEconomyAccessor accessor,
        ShopService shop,
        ICharacterProfileRepository profiles)
    {
        _economy = economy;
        _accessor = accessor;
        _shop = shop;
        _profiles = profiles;
    }

    [SlashCommand("furto", "Comete um furto rápido nas ruas (sem cooldown)")]
    public async Task FurtoAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var now = DateTime.UtcNow;
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (profile.RobCaughtUntil is { } caughtUntil && caughtUntil > now)
        {
            await RespondAsync(
                $"🚨 Você está sob custódia policial! Espere {FormatRemaining(caughtUntil - now)} ou pague sua fiança com `/crime fianca`.",
                ephemeral: true);
            return;
        }

        var (equipment, _, eqBonus, _) = await _shop.GetEquippedEquipmentAsync(Context.User.Id);
        var petBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Rob);

        bool success = CrimeRules.ShouldFurtoSucceed(Random.Shared, eqBonus);

        if (success)
        {
            ulong reward = CrimeRules.ComputeFurtoReward(Random.Shared, petBonus);
            await _economy.AddMoneyAsync(mainId, reward, EconomyTransactionType.Crime, "Furto rápido nas ruas");

            var sb = new StringBuilder();
            sb.AppendLine("Você se esgueirou pela multidão e bateu a carteira de um transeunte desprevenido!");
            sb.AppendLine();
            sb.AppendLine($"💰 **Lucro:** {EconomyFormat.Full(reward)} moedas");

            if (equipment != null && eqBonus > 0)
                sb.AppendLine($"🧤 **Equipamento:** {equipment.Emoji} {equipment.Name} (+{eqBonus}% discrição)");
            if (petBonus > 0)
                sb.AppendLine($"🥷 **Pet Batedor:** +{petBonus}% de lucro");

            var embed = new EmbedBuilder()
                .WithGoldTheme()
                .WithAuthor($"{Context.User.GetDisplayName()} — Furto Bem-Sucedido", Context.User.GetAvatarUrl())
                .WithDescription(sb.ToString())
                .WithStandardFooter("Sem cooldown! Mas cuidado: no flagrante policial você será preso.")
                .Build();

            await RespondAsync(embed: embed);
        }
        else
        {
            await _economy.SetRobAttemptAsync(mainId, now, now.Add(CrimeRules.PrisonLockout));
            ulong fine = CrimeRules.ComputeFine(profile.Money);

            if (profile.Money > 0)
            {
                var deduction = Math.Min(fine, profile.Money);
                await _economy.TryDeductMoneyAsync(mainId, deduction, EconomyTransactionType.Fine, "Multa por flagrante policial");
            }

            var sb = new StringBuilder();
            sb.AppendLine("🚨 **Você foi pego em flagrante pela polícia local!**");
            sb.AppendLine();
            sb.AppendLine("⏱️ **Pena:** 1 minuto na prisão sem poder cometer crimes.");
            sb.AppendLine($"💸 **Multa apreendida:** {EconomyFormat.Full(fine)} moedas");

            var embed = new EmbedBuilder()
                .WithColor(Color.DarkRed)
                .WithAuthor($"{Context.User.GetDisplayName()} — Preso em Flagrante!", Context.User.GetAvatarUrl())
                .WithDescription(sb.ToString())
                .WithStandardFooter("Use /crime fianca para pagar a taxa e sair da cela agora")
                .Build();

            await RespondAsync(embed: embed);
        }
    }

    [SlashCommand("roubar", "Assalta a carteira de outro usuário (1 min cooldown)")]
    public async Task RoubarAsync([Summary("usuario", "Usuário que deseja assaltar")] IUser target)
    {
        if (target.IsBot)
        {
            await RespondAsync("🤖 Robôs não carregam carteiras de dinheiro.", ephemeral: true);
            return;
        }

        if (target.Id == Context.User.Id)
        {
            await RespondAsync("😂 Você não pode assaltar a si mesmo.", ephemeral: true);
            return;
        }

        var attackerMain = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var victimMain = await _accessor.ResolveMainIdAsync(target.Id);

        if (attackerMain == victimMain)
        {
            await RespondAsync("🔗 A conta de destino faz parte do seu próprio grupo vinculado.", ephemeral: true);
            return;
        }

        var now = DateTime.UtcNow;
        var attacker = await _economy.GetOrCreateAsync(attackerMain, Context.User.Username);

        if (attacker.RobCaughtUntil is { } caughtUntil && caughtUntil > now)
        {
            await RespondAsync(
                $"🚨 Você está sob custódia policial! Espere {FormatRemaining(caughtUntil - now)} ou use `/crime fianca`.",
                ephemeral: true);
            return;
        }

        if (EconomyRules.GetRemainingCooldown(attacker.LastRobTime, now, CrimeRules.CrimeCooldown) is { } rem)
        {
            await RespondAsync($"⏳ Acalme-se... espere {FormatRemaining(rem)} para planejar outro assalto.", ephemeral: true);
            return;
        }

        var victim = await _economy.GetOrCreateAsync(victimMain, target.Username);

        if (victim.RobShieldUntil is { } shieldUntil && shieldUntil > now)
        {
            await RespondAsync(
                $"🛡️ **{target.GetDisplayName()}** está protegido pelo Escudo Anti-Roubo por mais {FormatRemaining(shieldUntil - now)}. Nem tente!",
                ephemeral: true);
            return;
        }

        if (victim.Money < 1)
        {
            await RespondAsync($"😅 **{target.GetDisplayName()}** está sem moedas na carteira.", ephemeral: true);
            return;
        }

        if (!await _economy.TryClaimRobAsync(attackerMain, now))
        {
            await RespondAsync("🚨 Acalme-se... seu golpe ainda está em cooldown.", ephemeral: true);
            return;
        }

        // Armas e Equipamentos
        var (attWeapon, attBonus, _, maxStealBonus) = await _shop.GetEquippedWeaponAsync(Context.User.Id);
        var (vicWeapon, _, vicDefense, _) = await _shop.GetEquippedWeaponAsync(target.Id);
        var (vicEquip, vicEquipType, _, vicEquipDef) = await _shop.GetEquippedEquipmentAsync(target.Id);
        var petBonus = await _shop.GetUpgradePercentAsync(attackerMain, UpgradeEffect.Rob);

        int totalVictimDefense = vicDefense + vicEquipDef;

        // Contra-ataque de autodefesa da vítima (ex: Taser ou defesa armada)
        if (CrimeRules.DidVictimCounterAttack(Random.Shared, vicDefense))
        {
            await _economy.SetRobAttemptAsync(attackerMain, now, now.Add(CrimeRules.PrisonLockout));
            ulong fine = CrimeRules.ComputeFine(attacker.Money);

            if (attacker.Money > 0)
                await _economy.TryDeductMoneyAsync(attackerMain, Math.Min(fine, attacker.Money), EconomyTransactionType.Fine, "Multa por assalto frustrado");

            ulong compensation = fine / 2;
            if (compensation > 0)
                await _economy.AddMoneyAsync(victimMain, compensation, EconomyTransactionType.Payment, $"Indenização por defesa armada de {Context.User.GetDisplayName()}");

            var sbCounter = new StringBuilder();
            sbCounter.AppendLine($"⚡ **Legítima Defesa!** **{target.GetDisplayName()}** reagiu prontamente");
            if (vicWeapon != null)
                sbCounter.AppendLine($"usando {vicWeapon.Emoji} **{vicWeapon.Name}** e neutralizou você no ato!");
            else
                sbCounter.AppendLine("e desarmou seu golpe!");
            sbCounter.AppendLine();
            sbCounter.AppendLine("👮 A polícia chegou e te levou em custódia.");
            sbCounter.AppendLine("⏱️ **Pena:** 1 minuto de prisão.");
            sbCounter.AppendLine($"💸 **Multa:** {EconomyFormat.Full(fine)} moedas ({EconomyFormat.Full(compensation)} entregues à vítima como reparação).");

            var counterEmbed = new EmbedBuilder()
                .WithColor(Color.DarkBlue)
                .WithAuthor($"{Context.User.GetDisplayName()} — Assalto Neutralizado!", Context.User.GetAvatarUrl())
                .WithDescription(sbCounter.ToString())
                .WithStandardFooter("Use /crime fianca para sair da cadeia imediatamente")
                .Build();

            await RespondAsync(embed: counterEmbed);
            return;
        }

        bool success = CrimeRules.ShouldRobSucceed(Random.Shared, attBonus, totalVictimDefense);

        if (success)
        {
            ulong stealCap = CrimeRules.BaseRobMaxSteal + maxStealBonus;
            ulong stolen = CrimeRules.ComputeRobAmount(victim.Money, stealCap, petBonus);

            if (vicEquipType == EquipmentType.Colete)
                stolen = Math.Max(1, stolen / 2);

            var (victimDeducted, _) = await _economy.TryDeductMoneyAsync(
                victimMain, stolen, EconomyTransactionType.Rob,
                $"Assaltado por {Context.User.GetDisplayName()}");

            if (!victimDeducted)
            {
                await RespondAsync("😅 A vítima já não possui moedas suficientes na carteira.", ephemeral: true);
                return;
            }

            await _economy.AddMoneyAsync(
                attackerMain, stolen, EconomyTransactionType.Rob,
                $"Roubou {EconomyFormat.Full(stolen)} de {target.GetDisplayName()}");

            var sbSuccess = new StringBuilder();
            sbSuccess.AppendLine($"Você rendeu **{target.GetDisplayName()}** e levou **{EconomyFormat.Full(stolen)} moedas**!");
            sbSuccess.AppendLine();

            if (attWeapon != null)
                sbSuccess.AppendLine($"🔫 **Arma utilizada:** {attWeapon.Emoji} {attWeapon.Name} (+{attBonus}% sucesso)");
            if (vicEquipType == EquipmentType.Colete)
                sbSuccess.AppendLine("🦺 **Aviso:** A vítima usava Colete Kevlar, reduzindo o roubo pela metade!");
            if (petBonus > 0)
                sbSuccess.AppendLine($"🥷 **Pet Batedor:** +{petBonus}% de lucro");

            var successEmbed = new EmbedBuilder()
                .WithGoldTheme()
                .WithAuthor($"{Context.User.GetDisplayName()} — Assalto Concluído!", Context.User.GetAvatarUrl())
                .WithDescription(sbSuccess.ToString())
                .WithStandardFooter("Cooldown de 1 minuto para o próximo assalto")
                .Build();

            await RespondAsync(embed: successEmbed);
        }
        else
        {
            await _economy.SetRobAttemptAsync(attackerMain, now, now.Add(CrimeRules.PrisonLockout));
            ulong fine = CrimeRules.ComputeFine(attacker.Money);

            bool alarmTriggered = vicEquipType == EquipmentType.Alarme;
            if (alarmTriggered)
                fine *= 2;

            if (attacker.Money > 0)
                await _economy.TryDeductMoneyAsync(attackerMain, Math.Min(fine, attacker.Money), EconomyTransactionType.Fine, "Multa por tentativa de assalto");

            ulong compensation = fine / 2;
            if (compensation > 0)
                await _economy.AddMoneyAsync(victimMain, compensation, EconomyTransactionType.Payment, $"Indenização paga por tentativa de assalto de {Context.User.GetDisplayName()}");

            var sbFail = new StringBuilder();
            sbFail.AppendLine($"🚨 **Você foi interceptado pela polícia durante a tentativa de assalto contra {target.GetDisplayName()}!**");
            sbFail.AppendLine();

            if (alarmTriggered)
                sbFail.AppendLine("🚨 **Alarme Residencial ativado!** O alarme sonoro da vítima disparou e a multa foi dobrada!");

            sbFail.AppendLine("⏱️ **Pena:** 1 minuto de prisão.");
            sbFail.AppendLine($"💸 **Multa:** {EconomyFormat.Full(fine)} moedas ({EconomyFormat.Full(compensation)} moedas entregues à vítima).");

            var failEmbed = new EmbedBuilder()
                .WithColor(Color.DarkRed)
                .WithAuthor($"{Context.User.GetDisplayName()} — Preso em Flagrante!", Context.User.GetAvatarUrl())
                .WithDescription(sbFail.ToString())
                .WithStandardFooter("Use /crime fianca para pagar com o banco e sair da prisão")
                .Build();

            await RespondAsync(embed: failEmbed);
        }
    }

    [SlashCommand("fianca", "Paga a fiança no banco para sair da prisão imediatamente")]
    public async Task FiancaAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var now = DateTime.UtcNow;
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (profile.RobCaughtUntil is not { } caughtUntil || caughtUntil <= now)
        {
            await RespondAsync("🕊️ Você não está preso no momento. Vá cometer crimes!", ephemeral: true);
            return;
        }

        ulong bailCost = CrimeRules.ComputeBail(profile.NetWorth);

        bool paid = false;

        if (profile.Money >= bailCost)
        {
            var (deducted, _) = await _economy.TryDeductMoneyAsync(mainId, bailCost, EconomyTransactionType.Bail, "Pagamento de fiança policial");
            paid = deducted;
        }
        else if (profile.Bank >= bailCost)
        {
            var (withdrawn, _, _) = await _economy.WithdrawAsync(mainId, bailCost);
            if (withdrawn)
            {
                var (deducted, _) = await _economy.TryDeductMoneyAsync(mainId, bailCost, EconomyTransactionType.Bail, "Pagamento de fiança policial (débito bancário)");
                paid = deducted;
            }
        }

        if (!paid)
        {
            await RespondAsync(
                $"❌ Você não tem saldo suficiente (carteira ou banco) para pagar a fiança de **{EconomyFormat.Full(bailCost)} moedas**.\n" +
                $"Aguarde o restante da pena: **{FormatRemaining(caughtUntil - now)}**.",
                ephemeral: true);
            return;
        }

        await _economy.SetRobAttemptAsync(mainId, profile.LastRobTime ?? now, null);

        var embed = new EmbedBuilder()
            .WithColor(Color.Green)
            .WithAuthor($"{Context.User.GetDisplayName()} — Liberdade Concedida", Context.User.GetAvatarUrl())
            .WithDescription(
                $"⚖️ **Fiança Paga com Sucesso!**\n\n" +
                $"Você pagou **{EconomyFormat.Full(bailCost)} moedas** e foi liberado da custódia policial.\n" +
                "Suas mãos estão livres para voltar à ativa!")
            .WithStandardFooter("Fique atento à polícia em suas próximas ações")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("arsenal", "Mostra a arma e equipamento atualmente equipados")]
    public async Task ArsenalAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);
        var now = DateTime.UtcNow;

        var (weapon, attBonus, defBonus, maxSteal) = await _shop.GetEquippedWeaponAsync(Context.User.Id);
        var (equipment, _, eqBonus, eqDef) = await _shop.GetEquippedEquipmentAsync(Context.User.Id);
        var (vehicle, _) = await _shop.GetCurrentVehicleAsync(Context.User.Id);
        var petBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Rob);

        var sb = new StringBuilder();

        sb.AppendLine("⚔️ **Arma Equipada:**");
        if (weapon != null)
        {
            sb.AppendLine($"   └ {weapon.Emoji} **{weapon.Name}**");
            sb.AppendLine($"   └ Ataque: **+{attBonus}%** sucesso em assaltos");
            sb.AppendLine($"   └ Defesa: **+{defBonus}%** contra assaltantes");
            sb.AppendLine($"   └ Teto de Roubo: **+{EconomyFormat.Full(maxSteal)}** moedas");
        }
        else
        {
            sb.AppendLine("   └ Nenhuma (desarmado). Compre armas na `/loja`.");
        }
        sb.AppendLine();

        sb.AppendLine("🧰 **Equipamento Utilitário:**");
        if (equipment != null)
        {
            sb.AppendLine($"   └ {equipment.Emoji} **{equipment.Name}**");
            if (eqBonus > 0)
                sb.AppendLine($"   └ Bônus: **+{eqBonus}%** em furtos/crimes");
            if (eqDef > 0)
                sb.AppendLine($"   └ Proteção: **+{eqDef}%** de defesa");
        }
        else
        {
            sb.AppendLine("   └ Nenhum. Compre equipamentos utilitários na `/loja`.");
        }
        sb.AppendLine();

        if (vehicle != null)
            sb.AppendLine($"🚗 **Veículo de Fuga:** {vehicle.Emoji} **{vehicle.Name}**");

        if (petBonus > 0)
            sb.AppendLine($"🥷 **Pet Batedor:** +{petBonus}% moedas em roubos");

        sb.AppendLine();
        sb.AppendLine("⚖️ **Status Criminal:**");
        if (profile.RobCaughtUntil is { } caughtUntil && caughtUntil > now)
            sb.AppendLine($"🚨 **PRESO** — {FormatRemaining(caughtUntil - now)} restantes. Use `/crime fianca`.");
        else
            sb.AppendLine("🕊️ **LIVRE** — Furtos sem cooldown liberados.");

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Arsenal Criminal", Context.User.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithStandardFooter("Equipe ou desequipe armas e itens em /inventario")
            .Build();

        await RespondAsync(embed: embed);
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}min";
        if (remaining.TotalMinutes >= 1)
            return $"{(int)remaining.TotalMinutes}min";
        return $"{Math.Max(1, remaining.Seconds)}seg";
    }
}
