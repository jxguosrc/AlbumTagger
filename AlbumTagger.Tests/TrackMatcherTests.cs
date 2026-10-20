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

    [Fact]
    public void Match_ExistingTrackNumbersOverrideFilenameOrder()
    {
        var files = new[]
        {
            new AudioFileReference("/album/Long.flac", "Long.flac", 10, ExistingTrackNumber: 2),
            new AudioFileReference("/album/Short.flac", "Short.flac", 10, ExistingTrackNumber: 1),
        };
        var tracks = new[]
        {
            new ParsedTrack(1, "Short"),
            new ParsedTrack(2, "Long"),
        };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.True(result.CanWrite);
        Assert.Equal(["Short.flac", "Long.flac"], result.Matches.Select(item => item.File.FileName));
    }

    [Fact]
    public void Match_PartialExistingTrackNumbers_BlocksWriting()
    {
        var files = new[]
        {
            new AudioFileReference("/album/Long.flac", "Long.flac", 10, ExistingTrackNumber: 2),
            new AudioFileReference("/album/Short.flac", "Short.flac", 10),
        };
        var tracks = new[] { new ParsedTrack(1, "Short"), new ParsedTrack(2, "Long") };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.False(result.CanWrite);
        Assert.Contains(result.Diagnostics, item => item.Code == "MATCH_PARTIAL_EXISTING_TRACK_NUMBERS");
    }

    [Fact]
    public void Match_DuplicateExistingTrackNumbers_BlocksWriting()
    {
        var files = new[]
        {
            new AudioFileReference("/album/A.flac", "A.flac", 10, ExistingTrackNumber: 1),
            new AudioFileReference("/album/B.flac", "B.flac", 10, ExistingTrackNumber: 1),
        };
        var tracks = new[] { new ParsedTrack(1, "First"), new ParsedTrack(2, "Second") };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.False(result.CanWrite);
        Assert.Contains(result.Diagnostics, item => item.Code == "MATCH_DUPLICATE_EXISTING_TRACK_NUMBERS");
    }

    [Fact]
    public void Match_NonSequentialExistingTrackNumbers_BlocksWriting()
    {
        var files = new[]
        {
            new AudioFileReference("/album/A.flac", "A.flac", 10, ExistingTrackNumber: 1),
            new AudioFileReference("/album/B.flac", "B.flac", 10, ExistingTrackNumber: 3),
        };
        var tracks = new[] { new ParsedTrack(1, "First"), new ParsedTrack(2, "Second") };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.False(result.CanWrite);
        Assert.Contains(result.Diagnostics, item => item.Code == "MATCH_NON_SEQUENTIAL_EXISTING_TRACK_NUMBERS");
    }

    [Fact]
    public void Match_CountMismatchWithExistingNumbers_PreviewsCorrectFileAndBlocksWriting()
    {
        var files = new[]
        {
            new AudioFileReference("/album/Long.flac", "Long.flac", 10, ExistingTrackNumber: 2),
            new AudioFileReference("/album/Short.flac", "Short.flac", 10, ExistingTrackNumber: 1),
        };
        var tracks = new[] { new ParsedTrack(1, "Short") };

        var result = new TrackMatcher().Match(files, tracks);

        Assert.False(result.CanWrite);
        Assert.Equal("Short.flac", Assert.Single(result.Matches).File.FileName);
        Assert.Contains(result.Diagnostics, item => item.Code == "MATCH_COUNT_MISMATCH");
    }
}
