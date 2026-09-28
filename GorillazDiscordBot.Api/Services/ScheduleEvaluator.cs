using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

public static class ScheduleEvaluator
{
    public static TimeZoneInfo SaoPauloTimeZone { get; } = GetSaoPauloTimeZone();

    private static TimeZoneInfo GetSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    public static bool ShouldFire(ScheduledSoundSettings? schedule, DateTime utcNow)
    {
        if (schedule is null || !schedule.Enabled)
            return false;

        if (schedule.Day.HasValue)
        {
            var saoPauloNow = TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.Utc, SaoPauloTimeZone);
            if (schedule.Day.Value != saoPauloNow.DayOfWeek)
                return false;
        }

        return utcNow.Hour == schedule.Time.Hours && utcNow.Minute == schedule.Time.Minutes;
    }

    public static bool TryParseSaoPaulo(string? input, out TimeSpan utcTime)
        => TryParseLocal(input, SaoPauloTimeZone, out utcTime);

    public static bool TryParseLocal(string? input, TimeZoneInfo timeZone, out TimeSpan utcTime)
    {
        utcTime = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!TimeSpan.TryParse(input.Trim(), out var local))
            return false;

        if (local < TimeSpan.Zero || local >= TimeSpan.FromDays(1))
            return false;

        var reference = new DateTime(2000, 7, 1, local.Hours, local.Minutes, 0, DateTimeKind.Unspecified);
        utcTime = local - timeZone.GetUtcOffset(reference);
        if (utcTime < TimeSpan.Zero)
            utcTime += TimeSpan.FromDays(1);

        return true;
    }

    public static string FormatSaoPauloTime(TimeSpan utc)
    {
        var local = utc + SaoPauloTimeZone.GetUtcOffset(DateTime.UtcNow);
        if (local < TimeSpan.Zero)
            local += TimeSpan.FromDays(1);
        if (local >= TimeSpan.FromDays(1))
            local -= TimeSpan.FromDays(1);

        return local.ToString(@"hh\:mm");
    }

    public static string FormatDayName(DayOfWeek? day)
        => day?.ToString() ?? "Todos os dias";
}