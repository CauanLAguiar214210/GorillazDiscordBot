using FluentAssertions;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public sealed class LocalSoundCatalogTests
{
    [Fact]
    public void List_ComPrefixoDoServidor_PreservaOCaminhoDoLavalink()
    {
        var directory = CreateDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "som.opus"), string.Empty);

            var result = LocalSoundCatalog.List(directory, "uploads");

            result.Should().ContainSingle()
                .Which.RelativePath.Should().Be(Path.Combine("uploads", "som.opus"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void List_IgnoraSomenteArquivosQueNaoSaoAudio()
    {
        var directory = CreateDirectory();
        try
        {
            File.WriteAllText(Path.Combine(directory, "som.aac"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "leia-me.txt"), string.Empty);

            var result = LocalSoundCatalog.List(directory);

            result.Should().ContainSingle().Which.FileName.Should().Be("som.aac");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gorillaz-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
