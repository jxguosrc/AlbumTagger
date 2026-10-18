using AlbumTagger.Core.Files;

namespace AlbumTagger.Tests;

public sealed class NaturalStringComparerTests
{
    [Fact]
    public void Sort_PutsNumericRunsInNumericOrder()
    {
        var names = new[] { "Track10.flac", "Track2.flac", "Track01.flac", "Track11.flac" };

        var sorted = names.OrderBy(name => name, NaturalStringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Equal(["Track01.flac", "Track2.flac", "Track10.flac", "Track11.flac"], sorted);
    }

    [Fact]
    public void Sort_IsCaseInsensitiveOutsideNumericRuns()
    {
        var names = new[] { "b2.flac", "A10.flac", "a2.flac" };

        var sorted = names.OrderBy(name => name, NaturalStringComparer.OrdinalIgnoreCase).ToArray();

        Assert.Equal(["a2.flac", "A10.flac", "b2.flac"], sorted);
    }
}
