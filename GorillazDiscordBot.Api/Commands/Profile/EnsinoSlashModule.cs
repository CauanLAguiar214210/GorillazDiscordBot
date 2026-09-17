using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Profile;

[Group("ensino", "Provas de matematica para evoluir sua escolaridade e diplomas de faculdade")]
public class EnsinoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    // prefix para provas de escolaridade (matematica)
    private const string EnsPrefix    = "ensino";
    private const string EnsAnswer    = "answer";
    private const string EnsCancel    = "cancel";
    private const string EnsRetry     = "retry";

    // prefix para faculdade (diplomas de emprego)
    private const string FacPrefix    = "fac";
    private const string FacSel       = "sel";
    private const string FacStart     = "start";
    private const string FacBack      = "back";
    private const string FacAnswer    = "answer";
    private const string FacCancel    = "cancel";
    private const string FacRetry     = "retry";

    private readonly ICharacterProfileRepository _profiles;
    private readonly QuizSessionService _quiz;
    private readonly JobExamSessionService _jobExam;
    private readonly IEconomyAccessor _accessor;

    public EnsinoSlashModule(
        ICharacterProfileRepository profiles,
        QuizSessionService quiz,
        JobExamSessionService jobExam,
        IEconomyAccessor accessor)
    {
        _profiles = profiles;
        _quiz     = quiz;
        _jobExam  = jobExam;
        _accessor = accessor;
    }

    // ──────────────────────────────────────────────────────────────
    // /ensino ver
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("ver", "Mostra seu nivel escolar e a proxima prova")]
    public async Task VerAsync()
    {
        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var next    = SchoolingProgression.Next(profile.Escolaridade);

        var sb = new StringBuilder();
        sb.AppendLine($"\U0001f393 **Nivel atual:** {FormatLevel(profile.Escolaridade)}");
        sb.AppendLine();
        sb.AppendLine(next != null
            ? $"\U0001f4dd Proxima prova: **{FormatLevel(next.Value)}** \u2014 use `/ensino prova`."
            : "\U0001f3c6 Voce concluiu todos os niveis de ensino!");

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} \u2014 Ensino", Context.User.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithStandardFooter("Acerte 2 de 3 questoes para passar")
            .Build();

        await RespondAsync(embed: embed);
    }

    // ──────────────────────────────────────────────────────────────
    // /ensino prova — prova de escolaridade (matematica)
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("prova", "Faz a prova de matematica do proximo nivel")]
    public async Task ProvaAsync()
    {
        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var next    = SchoolingProgression.Next(profile.Escolaridade);

        if (next == null)
        {
            await RespondAsync("\U0001f3c6 Voce ja concluiu todos os niveis de ensino!", ephemeral: true);
            return;
        }

        if (!_quiz.TryStart(mainId, next.Value, out var session))
        {
            await RespondAsync("\U0001f4dd Voce ja tem uma prova em andamento! Termine ou cancele antes de comecar outra.", ephemeral: true);
            return;
        }

        await RespondAsync(
            embed:      BuildSchoolQuestionEmbed(Context.User, session),
            components: BuildSchoolQuestionComponents(Context.User.Id, session));
    }

    // ──────────────────────────────────────────────────────────────
    // Handlers de quiz de escolaridade
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(EnsPrefix + ":" + EnsAnswer + ":*:*", true)]
    public async Task SchoolAnswerAsync(ulong ownerId, int optionIndex)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Esta prova nao e sua.", ephemeral: true);
            return;
        }

        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var session = _quiz.Get(mainId);

        if (session == null || session.IsFinished)
        {
            await FollowupAsync("\u231b Esta prova expirou. Use `/ensino prova` para comecar outra.", ephemeral: true);
            return;
        }

        if (session.Current.CorrectIndex == optionIndex)
            session.CorrectCount++;

        session.CurrentIndex++;

        if (!session.IsFinished)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed      = BuildSchoolQuestionEmbed(Context.User, session);
                m.Components = BuildSchoolQuestionComponents(Context.User.Id, session);
            });
            return;
        }

        var passed = session.Passed;
        if (passed)
        {
            var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
            profile.Escolaridade = session.Level;
            await _profiles.SaveAsync(profile);
        }

        _quiz.Remove(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildSchoolResultEmbed(Context.User, session, passed);
            m.Components = passed
                ? new ComponentBuilder().Build()
                : BuildSchoolRetryComponents(Context.User.Id, session.Level);
        });
    }

    [ComponentInteraction(EnsPrefix + ":" + EnsRetry + ":*:*", true)]
    public async Task SchoolRetryAsync(ulong ownerId, int level)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Esta prova nao e sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _quiz.Remove(mainId);

        if (!_quiz.TryStart(mainId, (SchoolingLevel)level, out var session))
        {
            await FollowupAsync("\U0001f4dd Voce ja tem uma prova em andamento.", ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildSchoolQuestionEmbed(Context.User, session);
            m.Components = BuildSchoolQuestionComponents(Context.User.Id, session);
        });
    }

    [ComponentInteraction(EnsPrefix + ":" + EnsCancel + ":*", true)]
    public async Task SchoolCancelAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Esta prova nao e sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _quiz.Cancel(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = new EmbedBuilder()
                .WithGoldTheme()
                .WithDescription("Prova cancelada. Use `/ensino prova` quando quiser tentar de novo.")
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ──────────────────────────────────────────────────────────────
    // /ensino faculdade — lista cursos e seleciona para fazer prova
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("faculdade", "Lista os cursos de faculdade e permite fazer a prova de diploma")]
    public async Task FaculdadeAsync()
    {
        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        var embed      = BuildFacOverviewEmbed(Context.User, profile);
        var components = BuildFacSelectMenu(Context.User.Id, profile);

        await RespondAsync(embed: embed, components: components);
    }

    // ──────────────────────────────────────────────────────────────
    // SelectMenu — escolhe o curso
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(FacPrefix + ":" + FacSel + ":*", true)]
    public async Task FacSelectAsync(ulong ownerId)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta faculdade nao e sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var jobKey    = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(jobKey)) return;

        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var job     = EconomyJobs.FindByKey(jobKey);

        if (job == null)
        {
            await FollowupAsync("Curso nao encontrado.", ephemeral: true);
            return;
        }

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildFacDetailEmbed(Context.User, job, profile);
            m.Components = BuildFacDetailComponents(Context.User.Id, job, profile);
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Botao Voltar — volta ao overview de cursos
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(FacPrefix + ":" + FacBack + ":*", true)]
    public async Task FacBackAsync(ulong ownerId)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Esta faculdade nao e sua.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var mainId    = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile   = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildFacOverviewEmbed(Context.User, profile);
            m.Components = BuildFacSelectMenu(Context.User.Id, profile);
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Botao Fazer prova — inicia a prova do curso selecionado
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(FacPrefix + ":" + FacStart + ":*:*", true)]
    public async Task FacStartAsync(ulong ownerId, string jobKey)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Este botao nao e seu.", ephemeral: true);
            return;
        }

        var job = EconomyJobs.FindByKey(jobKey);
        if (job == null || !job.RequiresDiploma)
        {
            await RespondAsync("Curso nao encontrado.", ephemeral: true);
            return;
        }

        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        if (profile.Diplomas.Contains(job.Key))
        {
            await RespondAsync($"\U0001f4dc Voce ja tem o diploma de **{job.Name}**!", ephemeral: true);
            return;
        }

        if (profile.Escolaridade < job.MinSchooling)
        {
            await RespondAsync(
                $"\U0001f393 Voce precisa de **{FormatLevel(job.MinSchooling)}** antes de cursar **{job.Name}**.",
                ephemeral: true);
            return;
        }

        if (!_jobExam.TryStart(mainId, job.Key, out var session))
        {
            await RespondAsync("\U0001f4dd Voce ja tem uma prova em andamento! Termine ou cancele antes de comecar outra.", ephemeral: true);
            return;
        }

        await RespondAsync(
            embed:      BuildFacQuestionEmbed(Context.User, session),
            components: BuildFacQuestionComponents(Context.User.Id, session));
    }

    // ──────────────────────────────────────────────────────────────
    // Handlers de quiz de faculdade (prefix fac:)
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(FacPrefix + ":" + FacAnswer + ":*:*", true)]
    public async Task FacAnswerAsync(ulong ownerId, int optionIndex)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Esta prova nao e sua.", ephemeral: true);
            return;
        }

        var mainId  = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var session = _jobExam.Get(mainId);

        if (session == null || session.IsFinished)
        {
            await FollowupAsync("\u231b Esta prova expirou. Use `/ensino faculdade` para comecar outra.", ephemeral: true);
            return;
        }

        if (session.Current.CorrectIndex == optionIndex)
            session.CorrectCount++;

        session.CurrentIndex++;

        if (!session.IsFinished)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed      = BuildFacQuestionEmbed(Context.User, session);
                m.Components = BuildFacQuestionComponents(Context.User.Id, session);
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

        _jobExam.Remove(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildFacResultEmbed(Context.User, session, passed);
            m.Components = passed
                ? new ComponentBuilder().Build()
                : BuildFacRetryComponents(Context.User.Id, session.JobKey);
        });
    }

    [ComponentInteraction(FacPrefix + ":" + FacRetry + ":*:*", true)]
    public async Task FacRetryAsync(ulong ownerId, string jobKey)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Esta prova nao e sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _jobExam.Remove(mainId);

        if (!_jobExam.TryStart(mainId, jobKey, out var session))
        {
            await FollowupAsync("\U0001f4dd Voce ja tem uma prova em andamento.", ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = BuildFacQuestionEmbed(Context.User, session);
            m.Components = BuildFacQuestionComponents(Context.User.Id, session);
        });
    }

    [ComponentInteraction(FacPrefix + ":" + FacCancel + ":*", true)]
    public async Task FacCancelAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Esta prova nao e sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _jobExam.Cancel(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = new EmbedBuilder()
                .WithGoldTheme()
                .WithDescription("Prova cancelada. Use `/ensino faculdade` quando quiser tentar de novo.")
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de embed — escolaridade
    // ──────────────────────────────────────────────────────────────

    private static Embed BuildSchoolQuestionEmbed(IUser user, QuizSession session)
    {
        var q  = session.Current;
        var sb = new StringBuilder();
        sb.AppendLine($"**{q.Text}**");
        sb.AppendLine();
        for (var i = 0; i < q.Options.Count; i++)
            sb.AppendLine($"`{Letter(i)}` {q.Options[i]}");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Prova de Matematica", user.GetAvatarUrl())
            .WithTitle($"\U0001f393 {FormatLevel(session.Level)}")
            .WithDescription(sb.ToString())
            .WithFooter($"Questao {session.CurrentIndex + 1}/{session.Questions.Count} \u00b7 Acerte {QuizSessionService.PassingScore} de {session.Questions.Count}")
            .Build();
    }

    private static MessageComponent BuildSchoolQuestionComponents(ulong ownerId, QuizSession session)
    {
        var q       = session.Current;
        var builder = new ComponentBuilder();

        for (var i = 0; i < q.Options.Count && i < 4; i++)
            builder.WithButton(
                $"{Letter(i)}) {q.Options[i]}",
                $"{EnsPrefix}:{EnsAnswer}:{ownerId}:{i}",
                ButtonStyle.Primary);

        builder.WithButton("\u274c Cancelar", $"{EnsPrefix}:{EnsCancel}:{ownerId}", ButtonStyle.Danger);
        return builder.Build();
    }

    private static MessageComponent BuildSchoolRetryComponents(ulong ownerId, SchoolingLevel level)
        => new ComponentBuilder()
            .WithButton("\U0001f501 Tentar de novo", $"{EnsPrefix}:{EnsRetry}:{ownerId}:{(int)level}", ButtonStyle.Success)
            .Build();

    private static Embed BuildSchoolResultEmbed(IUser user, QuizSession session, bool passed)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\u2705 Acertos: **{session.CorrectCount}/{session.Questions.Count}**");
        sb.AppendLine();
        sb.AppendLine(passed
            ? $"\U0001f389 **Aprovado!** Sua escolaridade agora e **{FormatLevel(session.Level)}**."
            : $"\u274c **Reprovado.** Acerte {QuizSessionService.PassingScore} de {session.Questions.Count} para passar.");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Prova de Matematica", user.GetAvatarUrl())
            .WithTitle(passed ? "\U0001f393 Aprovado!" : "\U0001f4dd Reprovado")
            .WithDescription(sb.ToString())
            .Build();
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de embed — faculdade
    // ──────────────────────────────────────────────────────────────

    private static Embed BuildFacOverviewEmbed(IUser user, CharacterProfile profile)
    {
        var courses = EconomyJobs.Empregos.Where(j => j.RequiresDiploma).ToList();
        var sb      = new StringBuilder();

        if (courses.Count == 0)
        {
            sb.AppendLine("Nenhum curso disponivel no momento.");
        }
        else
        {
            foreach (var job in courses)
            {
                var status = CourseStatus(job, profile);
                sb.AppendLine($"{job.Emoji} **{job.Name}**");
                sb.AppendLine($"   \u2514 {status}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("Selecione um curso abaixo para ver os detalhes.");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Faculdade", user.GetAvatarUrl())
            .WithTitle("\U0001f3eb Cursos Disponiveis")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Acerte 2 de 3 questoes para se formar")
            .Build();
    }

    private static Embed BuildFacDetailEmbed(IUser user, Job job, CharacterProfile profile)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{job.Emoji} **{job.Name}**");
        sb.AppendLine($"   \u2514 {job.Name ?? "Diploma profissional."}");
        sb.AppendLine();

        sb.AppendLine("**Requisitos**");
        sb.AppendLine($"   \u2514 \U0001f393 Escolaridade: **{FormatLevel(job.MinSchooling)}**");
        sb.AppendLine();

        sb.AppendLine("**Status**");
        sb.AppendLine($"   \u2514 {CourseStatus(job, profile)}");
        sb.AppendLine();

        sb.AppendLine($"\U0001f4bc Desbloqueia o emprego **{job.Name}** em `/trabalho trabalhar {job.Key}`.");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Faculdade", user.GetAvatarUrl())
            .WithTitle($"\U0001f393 {job.Name}")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Acerte 2 de 3 questoes para se formar")
            .Build();
    }

    private static Embed BuildFacQuestionEmbed(IUser user, JobExamSession session)
    {
        var q   = session.Current;
        var job = EconomyJobs.FindByKey(session.JobKey);
        var sb  = new StringBuilder();

        sb.AppendLine($"**{q.Text}**");
        sb.AppendLine();
        for (var i = 0; i < q.Options.Count; i++)
            sb.AppendLine($"`{Letter(i)}` {q.Options[i]}");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Faculdade", user.GetAvatarUrl())
            .WithTitle(job != null ? $"{job.Emoji} Prova \u2014 {job.Name}" : "\U0001f4dc Prova de Diploma")
            .WithDescription(sb.ToString())
            .WithFooter($"Questao {session.CurrentIndex + 1}/{session.Questions.Count} \u00b7 Acerte {JobLicensing.PassingScore} de {session.Questions.Count}")
            .Build();
    }

    private static Embed BuildFacResultEmbed(IUser user, JobExamSession session, bool passed)
    {
        var job  = EconomyJobs.FindByKey(session.JobKey);
        var name = job?.Name ?? session.JobKey;
        var sb   = new StringBuilder();

        sb.AppendLine($"\u2705 Acertos: **{session.CorrectCount}/{session.Questions.Count}**");
        sb.AppendLine();
        sb.AppendLine(passed
            ? $"\U0001f389 **Formado!** Voce obteve o diploma de **{name}** e ja pode trabalhar como `{session.JobKey}`."
            : $"\u274c **Reprovado.** Acerte {JobLicensing.PassingScore} de {session.Questions.Count} para se formar. Tente novamente!");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Faculdade", user.GetAvatarUrl())
            .WithTitle(passed ? "\U0001f393 Formado!" : "\U0001f4dd Reprovado")
            .WithDescription(sb.ToString())
            .Build();
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de componentes — faculdade
    // ──────────────────────────────────────────────────────────────

    private static MessageComponent BuildFacSelectMenu(ulong userId, CharacterProfile profile)
    {
        var courses = EconomyJobs.Empregos.Where(j => j.RequiresDiploma).ToList();

        var options = courses.Select(job =>
        {
            var formed  = profile.Diplomas.Contains(job.Key);
            var locked  = profile.Escolaridade < job.MinSchooling;
            var desc    = formed ? "Formado" : locked ? $"Requer {FormatLevel(job.MinSchooling)}" : "Disponivel";
            var label   = formed ? $"{job.Emoji} {job.Name} \u2705" : $"{job.Emoji} {job.Name}";
            return new SelectMenuOptionBuilder(label, job.Key, desc);
        }).ToList();

        if (options.Count == 0)
            options.Add(new SelectMenuOptionBuilder("\U0001f4da Nenhum curso disponivel", "none"));

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{FacPrefix}:{FacSel}:{userId}")
                .WithPlaceholder("Escolha um curso...")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildFacDetailComponents(ulong userId, Job job, CharacterProfile profile)
    {
        var formed = profile.Diplomas.Contains(job.Key);
        var locked = profile.Escolaridade < job.MinSchooling || !JobLicensing.HasExam(job.Key);

        var builder = new ComponentBuilder();

        builder.WithButton(new ButtonBuilder()
            .WithLabel(formed ? "\U0001f4dc Ja formado" : "\U0001f393 Fazer prova")
            .WithCustomId($"{FacPrefix}:{FacStart}:{userId}:{job.Key}")
            .WithStyle(formed ? ButtonStyle.Secondary : ButtonStyle.Success)
            .WithDisabled(formed || locked));

        builder.WithButton(new ButtonBuilder()
            .WithLabel("\u2b05 Voltar")
            .WithCustomId($"{FacPrefix}:{FacBack}:{userId}")
            .WithStyle(ButtonStyle.Secondary));

        return builder.Build();
    }

    private static MessageComponent BuildFacQuestionComponents(ulong ownerId, JobExamSession session)
    {
        var q       = session.Current;
        var builder = new ComponentBuilder();

        for (var i = 0; i < q.Options.Count && i < 4; i++)
            builder.WithButton(
                $"{Letter(i)}) {q.Options[i]}",
                $"{FacPrefix}:{FacAnswer}:{ownerId}:{i}",
                ButtonStyle.Primary);

        builder.WithButton("\u274c Cancelar", $"{FacPrefix}:{FacCancel}:{ownerId}", ButtonStyle.Danger);
        return builder.Build();
    }

    private static MessageComponent BuildFacRetryComponents(ulong ownerId, string jobKey)
        => new ComponentBuilder()
            .WithButton("\U0001f501 Tentar de novo", $"{FacPrefix}:{FacRetry}:{ownerId}:{jobKey}", ButtonStyle.Success)
            .Build();

    // ──────────────────────────────────────────────────────────────
    // Utilitarios
    // ──────────────────────────────────────────────────────────────

    private static string CourseStatus(Job job, CharacterProfile profile)
    {
        if (profile.Diplomas.Contains(job.Key))
            return "\u2705 Formado";
        if (profile.Escolaridade < job.MinSchooling)
            return $"\U0001f512 Requer **{FormatLevel(job.MinSchooling)}**";
        if (!JobLicensing.HasExam(job.Key))
            return "\U0001f512 Prova nao disponivel ainda";
        return "\U0001f4da Disponivel para cursar";
    }

    private static char Letter(int index) => (char)('A' + index);

    private static string FormatLevel(SchoolingLevel level) => level switch
    {
        SchoolingLevel.EnsinoFundamental1 => "Ensino Fundamental I",
        SchoolingLevel.EnsinoFundamental2 => "Ensino Fundamental II",
        SchoolingLevel.EnsinoMedio        => "Ensino Medio",
        SchoolingLevel.EnsinoSuperior     => "Ensino Superior",
        _                                 => "Nenhuma"
    };
}
