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

        var numberedFiles = files.Count(file => file.ExistingTrackNumber is > 0);
        if (numberedFiles == 0)
        {
            if (files.Any(file => file.ExistingTrackNumber is < 0))
            {
                diagnostics.Add(new CoreDiagnostic(
                    "MATCH_INVALID_EXISTING_TRACK_NUMBER",
                    DiagnosticSeverity.Error,
                    "FLAC 现有轨号包含无效的负数；写入已被阻止。"));
            }

            if (files.Count > 0)
            {
                diagnostics.Add(new CoreDiagnostic(
                    "MATCH_FILENAME_ORDER_FALLBACK",
                    DiagnosticSeverity.Warning,
                    "文件没有可用的现有轨号；已按文件名顺序匹配，请在写入前核对每一行。"));
            }

            var fallbackMatches = files.Zip(tracks, (file, track) => new TrackFileMatch(file, track));
            return new TrackMatchResult(fallbackMatches, diagnostics);
        }

        if (numberedFiles != files.Count)
        {
            diagnostics.Add(new CoreDiagnostic(
                "MATCH_PARTIAL_EXISTING_TRACK_NUMBERS",
                DiagnosticSeverity.Error,
                "只有部分 FLAC 带有有效轨号；无法可靠匹配，写入已被阻止。"));
            return new TrackMatchResult(
                files.Zip(tracks, (file, track) => new TrackFileMatch(file, track)),
                diagnostics);
        }

        var duplicateNumbers = files.GroupBy(file => file.ExistingTrackNumber!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateNumbers.Length > 0)
        {
            diagnostics.Add(new CoreDiagnostic(
                "MATCH_DUPLICATE_EXISTING_TRACK_NUMBERS",
                DiagnosticSeverity.Error,
                $"FLAC 现有轨号重复：{string.Join("、", duplicateNumbers.Select(number => number.ToString("00")))}；写入已被阻止。"));
        }

        var missingNumbers = Enumerable.Range(1, files.Count)
            .Except(files.Select(file => file.ExistingTrackNumber!.Value))
            .ToArray();
        var hasOutOfRangeNumber = files.Any(file => file.ExistingTrackNumber > files.Count);
        if (missingNumbers.Length > 0 || hasOutOfRangeNumber)
        {
            diagnostics.Add(new CoreDiagnostic(
                "MATCH_NON_SEQUENTIAL_EXISTING_TRACK_NUMBERS",
                DiagnosticSeverity.Error,
                "FLAC 现有轨号不是从 01 开始的完整连续序列；写入已被阻止。"));
        }

        if (duplicateNumbers.Length > 0 || missingNumbers.Length > 0 || hasOutOfRangeNumber)
        {
            return new TrackMatchResult(
                files.Zip(tracks, (file, track) => new TrackFileMatch(file, track)),
                diagnostics);
        }

        var filesByNumber = files.ToDictionary(file => file.ExistingTrackNumber!.Value);
        var matches = new List<TrackFileMatch>();
        foreach (var track in tracks)
        {
            if (filesByNumber.TryGetValue(track.TrackNumber, out var file))
            {
                matches.Add(new TrackFileMatch(file, track));
            }
            else
            {
                diagnostics.Add(new CoreDiagnostic(
                    "MATCH_TRACK_NUMBER_NOT_FOUND",
                    DiagnosticSeverity.Error,
                    $"找不到现有轨号为 {track.TrackNumber:00} 的 FLAC；写入已被阻止。"));
            }
        }

        return new TrackMatchResult(matches, diagnostics);
    }
}
