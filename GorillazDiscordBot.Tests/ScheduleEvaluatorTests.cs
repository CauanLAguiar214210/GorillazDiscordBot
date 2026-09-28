using FluentAssertions;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class ScheduleEvaluatorTests
{
    private static readonly DateTime ReferenceNow = new(2026, 9, 25, 17, 30, 0, DateTimeKind.Utc); // sexta

    [Fact]
    public void Desativado_NuncaDispara()
    {
        var schedule = new ScheduledSoundSettings { Enabled = false, Time = ReferenceNow.TimeOfDay };

        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow).Should().BeFalse();
    }

    [Fact]
    public void DiaNull_DisparaEmQualquerDia()
    {
        var schedule = new ScheduledSoundSettings { Enabled = true, Day = null, Time = ReferenceNow.TimeOfDay };

        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow).Should().BeTrue();
        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public void DiaEspecificoExterno_NaoDispara()
    {
        var schedule = new ScheduledSoundSettings { Enabled = true, Day = DayOfWeek.Monday, Time = ReferenceNow.TimeOfDay };

        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow).Should().BeFalse();
    }

    [Fact]
    public void DiaEspecificoIgual_Dispara()
    {
        var schedule = new ScheduledSoundSettings { Enabled = true, Day = DayOfWeek.Friday, Time = ReferenceNow.TimeOfDay };

        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow).Should().BeTrue();
    }

    [Fact]
    public void MinutoOuHoraDiferente_NaoDispara()
    {
        var schedule = new ScheduledSoundSettings { Enabled = true, Time = new TimeSpan(17, 30, 0) };

        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow.AddMinutes(1)).Should().BeFalse();
        ScheduleEvaluator.ShouldFire(schedule, ReferenceNow.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void AgendamentoNoturno_ComparaODiaNoFusoDeBrasilia()
    {
        // "segunda 22:00 Brasília" = 01:00 (terça) UTC.
        var schedule = new ScheduledSoundSettings
        {
            Enabled = true,
            Day = DayOfWeek.Monday,
            Time = TimeSpan.FromHours(1)
        };

        // UTC terça 01:00 = Brasília segunda 22:00 → deve disparar.
        var utcNaTerca = new DateTime(2026, 9, 22, 1, 0, 0, DateTimeKind.Utc);
        ScheduleEvaluator.ShouldFire(schedule, utcNaTerca).Should().BeTrue();

        // UTC segunda 01:00 = Brasília domingo 22:00 → não deve disparar (regressão do bug antigo).
        var utcNaSegunda = new DateTime(2026, 9, 21, 1, 0, 0, DateTimeKind.Utc);
        ScheduleEvaluator.ShouldFire(schedule, utcNaSegunda).Should().BeFalse();
    }

    [Fact]
    public void AgendamentoManha_ComparaODiaNoFusoDeBrasilia()
    {
        // "sexta 10:00 Brasília" = 13:00 UTC mesmo dia.
        var schedule = new ScheduledSoundSettings
        {
            Enabled = true,
            Day = DayOfWeek.Friday,
            Time = TimeSpan.FromHours(13)
        };

        var utcNaSexta = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc);
        ScheduleEvaluator.ShouldFire(schedule, utcNaSexta).Should().BeTrue();

        var utcNoSabado = utcNaSexta.AddDays(1);
        ScheduleEvaluator.ShouldFire(schedule, utcNoSabado).Should().BeFalse();
    }

    [Fact]
    public void TryParseSaoPaulo_ConverteParaUtc()
    {
        ScheduleEvaluator.TryParseSaoPaulo("14:30", out var utc).Should().BeTrue();
        utc.Should().Be(TimeSpan.FromHours(17.5));

        ScheduleEvaluator.TryParseSaoPaulo("00:00", out var meiaNoite).Should().BeTrue();
        meiaNoite.Should().Be(TimeSpan.FromHours(3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("25:00")]
    [InlineData("24:00")]
    [InlineData("-01:00")]
    public void TryParseSaoPaulo_EntradaInvalida_Falha(string? input)
    {
        ScheduleEvaluator.TryParseSaoPaulo(input, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParseLocal_FusoPositivo_ComWrapAposMeiaNoite()
    {
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("teste+02", TimeSpan.FromHours(2), "t", "t");

        ScheduleEvaluator.TryParseLocal("01:00", plusTwo, out var utc).Should().BeTrue();
        utc.Should().Be(TimeSpan.FromHours(23));

        ScheduleEvaluator.TryParseLocal("23:00", plusTwo, out var noite).Should().BeTrue();
        noite.Should().Be(TimeSpan.FromHours(21));
    }

    [Fact]
    public void FormatSaoPauloTime_ConverteUtcParaBrasilia()
    {
        ScheduleEvaluator.FormatSaoPauloTime(TimeSpan.FromHours(17.5)).Should().Be("14:30");
        ScheduleEvaluator.FormatSaoPauloTime(TimeSpan.FromHours(2)).Should().Be("23:00");
    }
}