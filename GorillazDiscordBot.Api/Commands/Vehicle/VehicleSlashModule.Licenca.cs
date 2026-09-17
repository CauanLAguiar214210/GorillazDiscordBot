using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Vehicle;

public partial class VehicleSlashModule
{
    [Group("licenca", "Provas por grupo (terrestre, marítima e aérea)")]
    public class Licenca : InteractionModuleBase<SocketInteractionContext>
    {
    private const string CustomIdPrefix = "licenca";
    private const string AnswerAction = "answer";
    private const string CancelAction = "cancel";
    private const string RetryAction = "retry";

    private readonly ICharacterProfileRepository _profiles;
    private readonly IEconomyRepository _economy;
    private readonly LicencaExamSessionService _sessions;
    private readonly IEconomyAccessor _accessor;

    public Licenca(
        ICharacterProfileRepository profiles,
        IEconomyRepository economy,
        LicencaExamSessionService sessions,
        IEconomyAccessor accessor)
    {
        _profiles = profiles;
        _economy = economy;
        _sessions = sessions;
        _accessor = accessor;
    }

    [SlashCommand("ver", "Mostra sua cadeia de licenças agrupadas por prova")]
    public async Task VerAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        var sb = new StringBuilder();
        foreach (var domain in LicenseProgression.Domains)
        {
            sb.AppendLine($"**{DomainLabel(domain)}**");
            foreach (var level in LicenseProgression.Sequence(domain))
            {
                var info = LicenseProgression.Info(level);
                var cost = LicenseCosts.ExamCost(level);
                var status = BuildStatus(level, profile);
                var prereq = LicenseProgression.Prerequisite(level);
                var prereqText = prereq is null
                    ? string.Empty
                    : $" · requer {LicenseProgression.Info(prereq.Value).Emoji} {LicenseProgression.Info(prereq.Value).Name}";
                sb.AppendLine($"{status}{info.Emoji} **{info.Name}**: exige {FormatSchooling(info.MinSchooling)}{prereqText} · prova **{EconomyFormat.Full(cost)}**");
            }
            sb.AppendLine();
        }

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Licenças", Context.User.GetAvatarUrl())
            .WithTitle("🪪 Suas licenças")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Use /veiculo licenca prova <grupo> · Acerte 2 de 3 para passar")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("prova", "Inicia a prova da próxima licença pendente do grupo")]
    public async Task ProvaAsync(
        [Summary("grupo", "Grupo de provas: Terrestre, Marítima ou Aérea")] GrupoProva grupo)
    {
        var domain = grupo switch
        {
            GrupoProva.Terrestre => LicenseDomain.Terrestre,
            GrupoProva.Maritima => LicenseDomain.Maritima,
            _ => LicenseDomain.Aerea
        };

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        LicenseLevel? next = null;
        foreach (var level in LicenseProgression.Sequence(domain))
        {
            if (!profile.Licencas.Contains(level))
            {
                next = level;
                break;
            }
        }

        if (next is null)
        {
            await RespondAsync(embed: new EmbedBuilder()
                .WithGoldTheme()
                .WithTitle("🏁 Tudo em ordem!")
                .WithDescription($"Você já possui **todas** as licenças de {DomainLabel(domain)}.")
                .Build());
            return;
        }

        var message = await StartExamAsync(mainId, Context.User.Username, next.Value);

        if (message != null)
        {
            await RespondAsync(embed: new EmbedBuilder()
                .WithGoldTheme()
                .WithDescription(message)
                .Build());
            return;
        }

        var session = _sessions.Get(mainId)!;
        await RespondAsync(
            embed: BuildQuestionEmbed(Context.User, session),
            components: BuildQuestionComponents(Context.User.Id, session));
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
            await FollowupAsync("⌛ Esta prova expirou. Use `/veiculo licenca prova <grupo>` para começar outra.", ephemeral: true);
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
            if (!profile.Licencas.Contains(session.Level))
            {
                profile.Licencas.Add(session.Level);
                await _profiles.SaveAsync(profile);
            }
        }

        _sessions.Remove(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildResultEmbed(Context.User, session, passed);
            m.Components = passed
                ? new ComponentBuilder().Build()
                : BuildRetryComponents(Context.User.Id, session.Level);
        });
    }

    [ComponentInteraction(CustomIdPrefix + ":" + RetryAction + ":*:*", true)]
    public async Task RetryAsync(ulong ownerId, int level)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _sessions.Remove(mainId);

        var message = await StartExamAsync(mainId, Context.User.Username, (LicenseLevel)level);

        if (message != null)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = new EmbedBuilder()
                    .WithGoldTheme()
                    .WithDescription(message)
                    .Build();
                m.Components = new ComponentBuilder().Build();
            });
            return;
        }

        var session = _sessions.Get(mainId)!;
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
                .WithDescription("🚪 Prova cancelada. Use `/veiculo licenca prova <grupo>` quando quiser tentar de novo.")
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task<string?> StartExamAsync(ulong mainId, string username, LicenseLevel nivel)
    {
        var profile = await _profiles.GetOrCreateAsync(mainId, username);
        var info = LicenseProgression.Info(nivel);

        if (profile.Licencas.Contains(nivel))
            return $"🏁 Você já tem a licença **{info.Emoji} {info.Name}**!";

        var requiredSchooling = LicenseProgression.RequiredSchooling(nivel);
        if (profile.Escolaridade < requiredSchooling)
            return $"🎓 Você precisa de **{FormatSchooling(requiredSchooling)}** para tirar **{info.Name}**. Use `/ensino` para estudar.";

        var prerequisite = LicenseProgression.Prerequisite(nivel);
        if (prerequisite is not null && !profile.Licencas.Contains(prerequisite.Value))
        {
            var prereqInfo = LicenseProgression.Info(prerequisite.Value);
            return $"🔒 Você precisa primeiro da licença **{prereqInfo.Emoji} {prereqInfo.Name}** para tentar **{info.Name}**.";
        }

        if (!_sessions.TryStart(mainId, nivel, out _))
            return "📝 Você já tem uma prova em andamento! Termine ou cancele antes de começar outra.";

        var cost = LicenseCosts.ExamCost(nivel);
        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            mainId, cost, EconomyTransactionType.Purchase, $"Prova de licença {info.Name}");

        if (!deducted)
        {
            _sessions.Remove(mainId);
            return $"❌ Você não tem **{EconomyFormat.Full(cost)}** moedas. A prova custa **{EconomyFormat.Full(cost)}** por tentativa.";
        }

        return null;
    }

    private static string BuildStatus(LicenseLevel level, CharacterProfile profile)
    {
        if (profile.Licencas.Contains(level))
            return "✅ ";
        if (profile.Escolaridade < LicenseProgression.RequiredSchooling(level))
            return "🎓 ";
        var prerequisite = LicenseProgression.Prerequisite(level);
        if (prerequisite is not null && !profile.Licencas.Contains(prerequisite.Value))
            return "🔒 ";
        return "📝 ";
    }

    private static string DomainLabel(LicenseDomain domain) => domain switch
    {
        LicenseDomain.Terrestre => "🚗 Terrestre",
        LicenseDomain.Maritima => "⛵ Marítima",
        LicenseDomain.Aerea => "✈️ Aérea",
        _ => "❓"
    };

    private static Embed BuildQuestionEmbed(IUser user, LicencaExamSession session)
    {
        var question = session.Current;
        var info = LicenseProgression.Info(session.Level);

        var sb = new StringBuilder();
        sb.AppendLine($"**{question.Text}**");
        sb.AppendLine();
        for (var i = 0; i < question.Options.Count; i++)
            sb.AppendLine($"`{Letter(i)}` {question.Options[i]}");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Prova de Licença", user.GetAvatarUrl())
            .WithTitle($"{info.Emoji} {info.Name}")
            .WithDescription(sb.ToString())
            .WithFooter($"Questão {session.CurrentIndex + 1}/{session.Questions.Count} · Acerte {LicenseQuizzes.PassingScore} de {session.Questions.Count}")
            .Build();
    }

    private static MessageComponent BuildQuestionComponents(ulong ownerId, LicencaExamSession session)
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

    private static MessageComponent BuildRetryComponents(ulong ownerId, LicenseLevel level)
        => new ComponentBuilder()
            .WithButton("🔁 Tentar de novo", $"{CustomIdPrefix}:{RetryAction}:{ownerId}:{(int)level}", ButtonStyle.Success)
            .Build();

    private static Embed BuildResultEmbed(IUser user, LicencaExamSession session, bool passed)
    {
        var info = LicenseProgression.Info(session.Level);

        var sb = new StringBuilder();
        sb.AppendLine($"✅ Acertos: **{session.CorrectCount}/{session.Questions.Count}**");
        sb.AppendLine();
        sb.AppendLine(passed
            ? $"🎉 **Aprovado!** Você agora tem a licença **{info.Emoji} {info.Name}**!"
            : $"❌ **Reprovado.** Acerte {LicenseQuizzes.PassingScore} de {session.Questions.Count} para passar. Tente novamente!");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Prova de Licença", user.GetAvatarUrl())
            .WithTitle(passed ? "🪪 Aprovado!" : "📝 Reprovado")
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
    }
}

public enum GrupoProva
{
    [ChoiceDisplay("🚗 Terrestre")]
    Terrestre,
    [ChoiceDisplay("⛵ Marítima")]
    Maritima,
    [ChoiceDisplay("✈️ Aérea")]
    Aerea
}