using FluentAssertions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Tests;

public class AudioUploadServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "gorillaz-upload-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Cleanup_ApagaSomenteArquivosGeradosEAntigos()
    {
        var service = CreateService();
        Directory.CreateDirectory(_directory);

        var antigoGerado = Path.Combine(_directory, $"{Guid.NewGuid():N}.mp3");
        var novoGerado = Path.Combine(_directory, $"{Guid.NewGuid():N}.ogg");
        var antigoEstranho = Path.Combine(_directory, "musica-do-admin.mp3");
        var parcialAntigo = $"{Guid.NewGuid():N}.mp3.part";

        await File.WriteAllTextAsync(antigoGerado, "a");
        await File.WriteAllTextAsync(novoGerado, "b");
        await File.WriteAllTextAsync(antigoEstranho, "c");
        await File.WriteAllTextAsync(Path.Combine(_directory, parcialAntigo), "d");

        File.SetLastWriteTimeUtc(antigoGerado, DateTime.UtcNow.AddDays(-40));
        File.SetLastWriteTimeUtc(antigoEstranho, DateTime.UtcNow.AddDays(-40));
        File.SetLastWriteTimeUtc(Path.Combine(_directory, parcialAntigo), DateTime.UtcNow.AddDays(-40));

        var removed = await service.CleanupAsync();

        removed.Should().Be(2);
        File.Exists(antigoGerado).Should().BeFalse();
        File.Exists(Path.Combine(_directory, parcialAntigo)).Should().BeFalse();
        File.Exists(novoGerado).Should().BeTrue();
        File.Exists(antigoEstranho).Should().BeTrue("arquivo que o bot não criou não é apagado");
    }

    [Fact]
    public async Task Cleanup_PastaInexistente_DevolveZero()
        => (await CreateService().CleanupAsync()).Should().Be(0);

    private AudioUploadService CreateService()
        => new(
            Options.Create(new AudioUploadOptions { Path = _directory }),
            NullLogger<AudioUploadService>.Instance);
}
