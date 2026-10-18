using AlbumTagger.Core.Files;
using AlbumTagger.Core.Parsing;

namespace AlbumTagger.Tests;

public sealed class TrackMatcherTests
{
    [Fact]
    public void Match_EqualCounts_PairsItemsByDisplayedOrder()
    {
        var files = new[]
        {
            new AudioFileReference("/album/01.flac", "01.flac", 10),
            new AudioFileReference("/album/02.flac", "02.flac", 10),
        };
        var tracks = new[]
        {
            new ParsedTrack(1, "First"),
            new ParsedTrack(2, "Second"),
        };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.True(result.CanWrite);
        Assert.Equal("First", result.Matches[0].Track.Title);
        Assert.Equal("01.flac", result.Matches[0].File.FileName);
    }

    [Fact]
    public void Match_DifferentCounts_ReturnsPreviewPairsButBlocksWriting()
    {
        var files = new[]
        {
            new AudioFileReference("/album/01.flac", "01.flac", 10),
            new AudioFileReference("/album/02.flac", "02.flac", 10),
        };
        var tracks = new[] { new ParsedTrack(1, "First") };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.False(result.CanWrite);
        Assert.Single(result.Matches);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "MATCH_COUNT_MISMATCH");
    }
}
