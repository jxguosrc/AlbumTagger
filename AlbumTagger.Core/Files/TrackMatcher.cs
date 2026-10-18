using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Parsing;

namespace AlbumTagger.Core.Files;

public sealed record TrackFileMatch(AudioFileReference File, ParsedTrack Track);

public sealed class TrackMatchResult
{
    public TrackMatchResult(
        IEnumerable<TrackFileMatch> matches,
        IEnumerable<CoreDiagnostic> diagnostics)
    {
        Matches = Array.AsReadOnly(matches.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
    }

    public IReadOnlyList<TrackFileMatch> Matches { get; }

    public IReadOnlyList<CoreDiagnostic> Diagnostics { get; }

    public bool CanWrite => Matches.Count > 0
        && Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

public sealed class TrackMatcher
{
    public TrackMatchResult Match(
        IReadOnlyList<AudioFileReference> files,
        IReadOnlyList<ParsedTrack> tracks)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(tracks);

        var diagnostics = new List<CoreDiagnostic>();
        if (files.Count == 0)
        {
            diagnostics.Add(new CoreDiagnostic(
                "MATCH_NO_FILES",
                DiagnosticSeverity.Error,
                "没有找到 FLAC 文件。"));
        }

        if (tracks.Count == 0)
        {
            diagnostics.Add(new CoreDiagnostic(
                "MATCH_NO_TRACKS",
                DiagnosticSeverity.Error,
                "没有解析到曲目。"));
        }

        if (files.Count != tracks.Count)
        {
            diagnostics.Add(new CoreDiagnostic(
                "MATCH_COUNT_MISMATCH",
                DiagnosticSeverity.Error,
                $"FLAC 数量为 {files.Count}，曲目数量为 {tracks.Count}；写入已被阻止。"));
        }

        var matches = files.Zip(tracks, (file, track) => new TrackFileMatch(file, track)).ToArray();
        return new TrackMatchResult(matches, diagnostics);
    }
}
