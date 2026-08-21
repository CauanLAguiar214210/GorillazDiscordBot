namespace GorillazDiscordBot.Domain.Interfaces;

public interface IAudioFileStorage
{
    Task<string> SaveAsync(Stream audioStream, string fileName);
    Task<Stream?> OpenReadAsync(string fileId);
    Task DeleteAsync(string fileId);
}
