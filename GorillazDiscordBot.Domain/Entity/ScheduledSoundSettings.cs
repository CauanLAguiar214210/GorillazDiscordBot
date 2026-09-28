namespace GorillazDiscordBot.Entity;

public class ScheduledSoundSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AudioSource { get; set; } = string.Empty;
    public DayOfWeek? Day { get; set; }
    public TimeSpan Time { get; set; }
    public ulong VoiceChannelId { get; set; }
    public bool Enabled { get; set; } = true;
    public int TimesPlayed { get; set; }
}