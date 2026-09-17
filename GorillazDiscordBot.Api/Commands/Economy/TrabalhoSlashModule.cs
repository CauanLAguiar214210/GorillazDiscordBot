using System.Text;
using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Economy;

[Group("trabalho", "Trabalhe, tire diplomas e evolua sua carreira")]
public class TrabalhoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string CustomIdPrefix = "trabalho";
    private const string AnswerAction = "answer";
    private const string CancelAction = "cancel";
    private const string RetryAction = "retry";

    private readonly ICharacterProfileRepository _profiles;
    private readonly IEconomyRepository _economy;
    private readonly JobExamSessionService _sessions;
    private readonly ShopService _shop;
    private readonly IEconomyAccessor _accessor;

    public TrabalhoSlashModule(
        ICharacterProfileRepository profiles,
        IEconomyRepository economy,
        JobExamSessionService sessions,
        ShopService shop,
        IEconomyAccessor accessor)
    {
        _profiles = profiles;
        _economy = economy;
        _sessions = sessions;
        _shop = shop;
        _accessor = accessor;
    }

    [SlashCommand("listar", "Lista os subempregos e empregos disponíveis")]
    public async Task ListarAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        var sb = new StringBuilder();
        sb.AppendLine("👷 **Subempregos** — sem diploma");
        foreach (var job in EconomyJobs.SubEmpregos)
            sb.AppendLine($"{job.Emoji} **{job.Name}** (`{job.Key}`) — {job.Hours}h → +**{EconomyFormat.Full(job.TotalPay)}** · {Status(job, profile)}");
        sb.AppendLine();
        sb.AppendLine("🎓 **Empregos** — exigem diploma");
        foreach (var job in EconomyJobs.Empregos)
            sb.AppendLine($"{job.Emoji} **{job.Name}** (`{job.Key}`) — {job.Hours}h → +**{EconomyFormat.Full(job.TotalPay)}** · {Requirement(job)} · {Status(job, profile)}");

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Trabalho", Context.User.GetAvatarUrl())
            .WithTitle("💼 Mercado de trabalho")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Use /trabalho trabalhar <profissão> · /trabalho prova <profissão>")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("trabalhar", "Trabalha em uma profissão e recebe o pagamento")]
    public async Task TrabalharAsync(
        [Summary("profissao", "Chave ou nome da profissão (ex.: entregador, programador)")] string profissao)
    {
        var job = EconomyJobs.Find(profissao);
        if (job == null)
        {
            await RespondAsync("❌ Profissão não encontrada. Use `/trabalho listar` para ver as opções.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        if (profile.Escolaridade < job.MinSchooling)
        {
            await RespondAsync(
                $"🎓 Você precisa de **{FormatSchooling(job.MinSchooling)}** para trabalhar como **{job.Name}**. Use `/ensino` para estudar.",
                ephemeral: true);
            return;
        }

        if (job.RequiresDiploma && !profile.Diplomas.Contains(job.Key))
        {
            await RespondAsync(
                $"📜 Você ainda não tem o diploma de **{job.Name}**. Use `/trabalho prova {job.Key}` para tirar a licença.",
                ephemeral: true);
            return;
        }

        var now = DateTime.UtcNow;
        var economy = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (EconomyRules.GetRemainingCooldown(economy.LastWorkTime, now, TimeSpan.FromHours(job.Hours)) is { } remaining)
        {
            await RespondAsync($"⏳ Você ainda está trabalhando! Espere {FormatRemaining(remaining)} para trabalhar como **{job.Name}**.", ephemeral: true);
            return;
        }

        var pay = job.TotalPay;
        var workBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Work);
        if (workBonus > 0)
            pay += pay * (ulong)workBonus / 100;

        var boost = economy.WorkBoostPending;
        if (boost && economy.WorkBoostExpiresAt is { } wExp && wExp <= DateTime.UtcNow)
        {
            await _economy.SetWorkBoostAsync(mainId, false);
            boost = false;
        }
        if (boost)
            pay *= 2;

        if (!await _economy.TryClaimWorkAsync(mainId, now, TimeSpan.FromHours(job.Hours)))
        {
            await RespondAsync("⏳ Você já está trabalhando neste momento. Aguarde o término para começar outro serviço.", ephemeral: true);
            return;
        }

        await _economy.AddMoneyAsync(
            mainId, pay, EconomyTransactionType.Work,
            $"Trabalhou como {job.Name} ({job.Hours}h)");

        if (boost)
            await _economy.SetWorkBoostAsync(mainId, false);

        var boostSuffix = boost ? " ⚡ **(com bônus x2!)**" : string.Empty;
        var bonusSuffix = workBonus > 0 ? $" 🐾 (+{workBonus}% pet)" : string.Empty;

        await RespondAsync($"💪 Você trabalhou como **{job.Emoji} {job.Name}** por {job.Hours}h e ganhou **{EconomyFormat.Full(pay)} moedas**!{bonusSuffix}{boostSuffix}");
    }

    [SlashCommand("prova", "Faz a prova de licença de um emprego que exige diploma")]
    public async Task ProvaAsync(
        [Summary("profissao", "Chave ou nome do emprego (ex.: programador, engenheiro)")] string profissao)
    {
        var job = EconomyJobs.Find(profissao);
        if (job == null || !job.RequiresDiploma)
        {
            await RespondAsync("❌ Essa profissão não exige diploma. Use `/trabalho listar` para ver as opções.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        if (profile.Diplomas.Contains(job.Key))
        {
            await RespondAsync($"📜 Você já tem o diploma de **{job.Name}**!", ephemeral: true);
            return;
        }

        if (profile.Escolaridade < job.MinSchooling)
        {
            await RespondAsync(
                $"🎓 Você precisa de **{FormatSchooling(job.MinSchooling)}** antes de tirar o diploma de **{job.Name}**.",
                ephemeral: true);
            return;
        }

        if (!_sessions.TryStart(mainId, job.Key, out var session))
        {
            await RespondAsync("📝 Você já tem uma prova em andamento! Termine ou cancele antes de começar outra.", ephemeral: true);
            return;
        }

        await RespondAsync(
            embed: BuildQuestionEmbed(Context.User, session),
            components: BuildQuestionComponents(Context.User.Id, session));
    }

    [SlashCommand("diplomas", "Mostra os diplomas que você já conquistou")]
    public async Task DiplomasAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        var sb = new StringBuilder();
        if (profile.Diplomas.Count == 0)
        {
            sb.AppendLine("Você ainda não tem nenhum diploma.");
            sb.AppendLine("Use `/trabalho prova <profissão>` para tirar o primeiro!");
        }
        else
        {
            foreach (var key in profile.Diplomas)
            {
                var job = EconomyJobs.FindByKey(key);
                sb.AppendLine(job != null
                    ? $"{job.Emoji} **{job.Name}** (`{job.Key}`)"
                    : $"📜 `{key}`");
            }
        }

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Diplomas", Context.User.GetAvatarUrl())
            .WithTitle("📜 Seus diplomas")
            .WithDescription(sb.ToString())
            .Build();

        await RespondAsync(embed: embed);
    }

    [ComponentInteraction(CustomIdPrefix + ":" + AnswerAction + ":*:*", true)]
    public async Task AnswerAsync(ulong ownerId, int optionIndex)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var session = _sessions.Get(mainId);

        if (session == null || session.IsFinished)
        {
            await FollowupAsync("⌛ Esta prova expirou. Use `/trabalho prova <profissão>` para começar outra.", ephemeral: true);
            return;
        }

        if (session.Current.CorrectIndex == optionIndex)
            session.CorrectCount++;

        session.CurrentIndex++;

        if (!session.IsFinished)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BuildQuestionEmbed(Context.User, session);
                m.Components = BuildQuestionComponents(Context.User.Id, session);
            });
            return;
        }

        var passed = session.Passed;
        if (passed)
        {
            var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
            if (!profile.Diplomas.Contains(session.JobKey))
            {
                profile.Diplomas.Add(session.JobKey);
                await _profiles.SaveAsync(profile);
            }
        }

        _sessions.Remove(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildResultEmbed(Context.User, session, passed);
            m.Components = passed
                ? new ComponentBuilder().Build()
                : BuildRetryComponents(Context.User.Id, session.JobKey);
        });
    }

    [ComponentInteraction(CustomIdPrefix + ":" + RetryAction + ":*:*", true)]
    public async Task RetryAsync(ulong ownerId, string jobKey)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _sessions.Remove(mainId);

        if (!_sessions.TryStart(mainId, jobKey, out var session))
        {
            await FollowupAsync("📝 Você já tem uma prova em andamento.", ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildQuestionEmbed(Context.User, session);
            m.Components = BuildQuestionComponents(Context.User.Id, session);
        });
    }

    [ComponentInteraction(CustomIdPrefix + ":" + CancelAction + ":*", true)]
    public async Task CancelAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _sessions.Cancel(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithGoldTheme()
                .WithDescription("🚪 Prova cancelada. Use `/trabalho prova <profissão>` quando quiser tentar de novo.")
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    private static string Status(Job job, CharacterProfile profile)
    {
        if (profile.Escolaridade < job.MinSchooling)
            return "🔒 escolaridade insuficiente";
        if (job.RequiresDiploma && !profile.Diplomas.Contains(job.Key))
            return "🔒 falta diploma";
        return "✅ disponível";
    }

    private static string Requirement(Job job)
        => job.RequiresDiploma
            ? $"requer diploma + {FormatSchooling(job.MinSchooling)}"
            : "sem requisitos";

    private static Embed BuildQuestionEmbed(IUser user, JobExamSession session)
    {
        var question = session.Current;
        var job = EconomyJobs.FindByKey(session.JobKey);
        var title = job != null ? $"{job.Emoji} Licença de {job.Name}" : "📜 Prova de licença";

        var sb = new StringBuilder();
        sb.AppendLine($"**{question.Text}**");
        sb.AppendLine();
        for (var i = 0; i < question.Options.Count; i++)
            sb.AppendLine($"`{Letter(i)}` {question.Options[i]}");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Prova de Licença", user.GetAvatarUrl())
            .WithTitle(title)
            .WithDescription(sb.ToString())
            .WithFooter($"Questão {session.CurrentIndex + 1}/{session.Questions.Count} · Acerte {JobLicensing.PassingScore} de {session.Questions.Count}")
            .Build();
    }

    private static MessageComponent BuildQuestionComponents(ulong ownerId, JobExamSession session)
    {
        var question = session.Current;
        var builder = new ComponentBuilder();

        for (var i = 0; i < question.Options.Count && i < 4; i++)
            builder.WithButton(
                $"{Letter(i)}) {question.Options[i]}",
                $"{CustomIdPrefix}:{AnswerAction}:{ownerId}:{i}",
                ButtonStyle.Primary);

        builder.WithButton("❌ Cancelar", $"{CustomIdPrefix}:{CancelAction}:{ownerId}", ButtonStyle.Danger);
        return builder.Build();
    }

    private static MessageComponent BuildRetryComponents(ulong ownerId, string jobKey)
        => new ComponentBuilder()
            .WithButton("🔁 Tentar de novo", $"{CustomIdPrefix}:{RetryAction}:{ownerId}:{jobKey}", ButtonStyle.Success)
            .Build();

    private static Embed BuildResultEmbed(IUser user, JobExamSession session, bool passed)
    {
        var job = EconomyJobs.FindByKey(session.JobKey);
        var name = job?.Name ?? session.JobKey;

        var sb = new StringBuilder();
        sb.AppendLine($"✅ Acertos: **{session.CorrectCount}/{session.Questions.Count}**");
        sb.AppendLine();
        sb.AppendLine(passed
            ? $"🎉 **Aprovado!** Você tirou o diploma de **{name}** e já pode trabalhar como `{session.JobKey}`."
            : $"❌ **Reprovado.** Acerte {JobLicensing.PassingScore} de {session.Questions.Count} para passar. Tente novamente!");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Prova de Licença", user.GetAvatarUrl())
            .WithTitle(passed ? "📜 Aprovado!" : "📝 Reprovado")
            .WithDescription(sb.ToString())
            .Build();
    }

    private static char Letter(int index) => (char)('A' + index);

    private static string FormatSchooling(SchoolingLevel level) => level switch
    {
        SchoolingLevel.EnsinoFundamental1 => "Ensino Fundamental I",
        SchoolingLevel.EnsinoFundamental2 => "Ensino Fundamental II",
        SchoolingLevel.EnsinoMedio => "Ensino Médio",
        SchoolingLevel.EnsinoSuperior => "Ensino Superior",
        _ => "Nenhuma"
    };

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}min";
        if (remaining.TotalMinutes >= 1)
            return $"{(int)remaining.TotalMinutes}min";
        return $"{remaining.Seconds}seg";
    }
}