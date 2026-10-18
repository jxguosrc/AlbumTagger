using AlbumTagger.Core.Files;

namespace AlbumTagger.Tests;

public sealed class FlacFileDiscoveryTests
{
    [Fact]
    public void Discover_FiltersFlacAndUsesNaturalOrder()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Path, "10.flac"), "fixture");
        File.WriteAllText(Path.Combine(directory.Path, "2.FLAC"), "fixture");
        File.WriteAllText(Path.Combine(directory.Path, "01.flac"), "fixture");
        File.WriteAllText(Path.Combine(directory.Path, "notes.txt"), "ignored");

        var files = new FlacFileDiscovery().Discover(directory.Path);

        Assert.Equal(["01.flac", "2.FLAC", "10.flac"], files.Select(file => file.FileName));
    }

    [Fact]
    public void Discover_DoesNotRecurseByDefault()
    {
        using var directory = new TemporaryDirectory();
        var child = Directory.CreateDirectory(Path.Combine(directory.Path, "Disc 2"));
        File.WriteAllText(Path.Combine(child.FullName, "01.flac"), "fixture");

        Assert.Empty(new FlacFileDiscovery().Discover(directory.Path));
        Assert.Single(new FlacFileDiscovery().Discover(directory.Path, recursive: true));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"AlbumTagger.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
