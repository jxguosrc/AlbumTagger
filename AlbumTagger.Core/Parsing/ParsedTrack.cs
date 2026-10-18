namespace AlbumTagger.Core.Parsing;

public sealed class ParsedTrack
{
    public ParsedTrack(
        int trackNumber,
        string title,
        IEnumerable<string>? metadataLines = null,
        int? sourceLine = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trackNumber);

        TrackNumber = trackNumber;
        Title = title ?? throw new ArgumentNullException(nameof(title));
        MetadataLines = Array.AsReadOnly((metadataLines ?? []).ToArray());
        SourceLine = sourceLine;
    }

    public int TrackNumber { get; }

    public string Title { get; }

    public IReadOnlyList<string> MetadataLines { get; }

    public string RawMetadata => string.Join("\n", MetadataLines);

    public int? SourceLine { get; }
}
