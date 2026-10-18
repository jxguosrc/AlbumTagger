using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Parsing;

namespace AlbumTagger.Tests;

public sealed class TracklistParserTests
{
    private readonly TracklistParser parser = new();

    [Fact]
    public void Parse_UrasiaSample_ProducesSixTracksAndKeepsFeatMetadata()
    {
        const string text = """
            TRACKLIST
            01
            Reason of Hope
            feat.咲良ゆの
            02
            E.X.P
            feat.kotono
            03
            Constellation Song
            feat.月乃
            04
            Reason of Hope (Instrumental)
            05
            E.X.P (Instrumental)
            06
            Constellation Song (Instrumental)
            CREDIT
            Produce / Compose / Design
            Yuu Ito
            """;

        var result = parser.Parse(text);

        Assert.False(result.HasErrors);
        Assert.Equal(TracklistParseMode.NumberedBlocks, result.Mode);
        Assert.Equal(6, result.Tracks.Count);
        Assert.Equal("Reason of Hope", result.Tracks[0].Title);
        Assert.Equal("feat.咲良ゆの", result.Tracks[0].RawMetadata);
        Assert.Equal("Constellation Song (Instrumental)", result.Tracks[5].Title);
        Assert.Empty(result.Tracks[5].MetadataLines);
    }

    [Theory]
    [InlineData("01. Song A\n02. Song B")]
    [InlineData("Tr.1 Song A\nTrack 2 Song B")]
    [InlineData("01、Song A\n02、Song B")]
    public void Parse_InlineNumberedFormats_RecognizesTitles(string text)
    {
        var result = parser.Parse(text);

        Assert.False(result.HasErrors);
        Assert.Equal(TracklistParseMode.InlineNumbered, result.Mode);
        Assert.Collection(
            result.Tracks,
            track => Assert.Equal("Song A", track.Title),
            track => Assert.Equal("Song B", track.Title));
    }

    [Fact]
    public void Parse_NumberedBlockWithoutMetadata_DoesNotConsumeNextTrack()
    {
        var result = parser.Parse("01\nFirst\n02\nSecond\n03\nThird");

        Assert.Equal(3, result.Tracks.Count);
        Assert.All(result.Tracks, track => Assert.Empty(track.MetadataLines));
    }

    [Fact]
    public void Parse_UnnumberedText_FallsBackToOneTrackPerLine()
    {
        var result = parser.Parse("First\nSecond");

        Assert.Equal(TracklistParseMode.OneTrackPerLine, result.Mode);
        Assert.Equal(["First", "Second"], result.Tracks.Select(track => track.Title));
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "PARSE_FALLBACK_ONE_PER_LINE"
                && diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Parse_DuplicateTrackNumbers_ReturnsBlockingError()
    {
        var result = parser.Parse("01\nFirst\n01\nAgain");

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "PARSE_DUPLICATE_TRACK_NUMBER"
            && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsBlockingError()
    {
        var result = parser.Parse(" \r\n ");

        Assert.True(result.HasErrors);
        Assert.Empty(result.Tracks);
    }
}
