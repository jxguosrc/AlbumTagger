using AlbumTagger.Core.Diagnostics;

namespace AlbumTagger.Core.Parsing;

public enum TracklistParseMode
{
    None,
    NumberedBlocks,
    InlineNumbered,
    OneTrackPerLine,
}

public sealed class TracklistParseResult
{
    public TracklistParseResult(
        TracklistParseMode mode,
        IEnumerable<ParsedTrack> tracks,
        IEnumerable<CoreDiagnostic> diagnostics)
    {
        Mode = mode;
        Tracks = Array.AsReadOnly(tracks.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public TracklistParseMode Mode { get; }

    public IReadOnlyList<ParsedTrack> Tracks { get; }

    public IReadOnlyList<CoreDiagnostic> Diagnostics { get; }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
