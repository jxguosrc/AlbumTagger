using System.Globalization;
using System.Text.RegularExpressions;
using AlbumTagger.Core.Diagnostics;

namespace AlbumTagger.Core.Parsing;

public sealed partial class TracklistParser
{
    private static readonly HashSet<string> TracklistHeadings = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRACKLIST",
        "TRACK LIST",
        "トラックリスト",
    };

    private static readonly HashSet<string> SectionTerminators = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREDIT",
        "CREDITS",
        "STAFF",
    };

    public TracklistParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TracklistParseResult(
                TracklistParseMode.None,
                [],
                [new CoreDiagnostic("PARSE_EMPTY", DiagnosticSeverity.Error, "曲目表为空。")]);
        }

        var lines = ReadSourceLines(text);
        lines = SliceTracklistSection(lines);

        if (lines.Count == 0)
        {
            return new TracklistParseResult(
                TracklistParseMode.None,
                [],
                [new CoreDiagnostic("PARSE_NO_CONTENT", DiagnosticSeverity.Error, "没有找到可解析的曲目内容。")]);
        }

        TracklistParseResult result;
        if (lines.Any(line => TryParseStandaloneTrackNumber(line.Text, out _)))
        {
            result = ParseNumberedBlocks(lines);
        }
        else if (lines.Any(line => TryParseInlineTrack(line.Text, out _, out _)))
        {
            result = ParseInlineNumbered(lines);
        }
        else
        {
            result = ParseOneTrackPerLine(lines);
        }

        return AddSequenceDiagnostics(result);
    }

    private static List<SourceLine> ReadSourceLines(string text)
    {
        var rawLines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var result = new List<SourceLine>(rawLines.Length);
        for (var index = 0; index < rawLines.Length; index++)
        {
            var value = rawLines[index].Trim();
            if (value.Length > 0)
            {
                result.Add(new SourceLine(index + 1, value));
            }
        }

        return result;
    }

    private static List<SourceLine> SliceTracklistSection(List<SourceLine> lines)
    {
        var headingIndex = lines.FindIndex(line => TracklistHeadings.Contains(line.Text));
        var startIndex = headingIndex >= 0 ? headingIndex + 1 : 0;
        var result = new List<SourceLine>();

        for (var index = startIndex; index < lines.Count; index++)
        {
            if (SectionTerminators.Contains(lines[index].Text))
            {
                break;
            }

            if (!TracklistHeadings.Contains(lines[index].Text))
            {
                result.Add(lines[index]);
            }
        }

        return result;
    }

    private static TracklistParseResult ParseNumberedBlocks(IReadOnlyList<SourceLine> lines)
    {
        var tracks = new List<ParsedTrack>();
        var diagnostics = new List<CoreDiagnostic>();
        TrackBlock? block = null;

        foreach (var line in lines)
        {
            if (TryParseStandaloneTrackNumber(line.Text, out var trackNumber))
            {
                FinalizeBlock(block, tracks, diagnostics);
                block = new TrackBlock(trackNumber, line.Number);
                continue;
            }

            if (block is null)
            {
                diagnostics.Add(new CoreDiagnostic(
                    "PARSE_IGNORED_PREAMBLE",
                    DiagnosticSeverity.Warning,
                    $"在第一条轨号前忽略了文本：{line.Text}",
                    line.Number));
                continue;
            }

            block.Content.Add(line.Text);
        }

        FinalizeBlock(block, tracks, diagnostics);
        return new TracklistParseResult(TracklistParseMode.NumberedBlocks, tracks, diagnostics);
    }

    private static TracklistParseResult ParseInlineNumbered(IReadOnlyList<SourceLine> lines)
    {
        var tracks = new List<ParsedTrack>();
        var diagnostics = new List<CoreDiagnostic>();
        TrackBlock? block = null;

        foreach (var line in lines)
        {
            if (TryParseInlineTrack(line.Text, out var trackNumber, out var title))
            {
                FinalizeBlock(block, tracks, diagnostics);
                block = new TrackBlock(trackNumber, line.Number);
                block.Content.Add(title);
                continue;
            }

            if (block is null)
            {
                diagnostics.Add(new CoreDiagnostic(
                    "PARSE_IGNORED_PREAMBLE",
                    DiagnosticSeverity.Warning,
                    $"在第一条带轨号曲目前忽略了文本：{line.Text}",
                    line.Number));
                continue;
            }

            block.Content.Add(line.Text);
        }

        FinalizeBlock(block, tracks, diagnostics);
        return new TracklistParseResult(TracklistParseMode.InlineNumbered, tracks, diagnostics);
    }

    private static TracklistParseResult ParseOneTrackPerLine(IReadOnlyList<SourceLine> lines)
    {
        var tracks = lines
            .Select((line, index) => new ParsedTrack(index + 1, line.Text, sourceLine: line.Number))
            .ToArray();

        return new TracklistParseResult(
            TracklistParseMode.OneTrackPerLine,
            tracks,
            [new CoreDiagnostic(
                "PARSE_FALLBACK_ONE_PER_LINE",
                DiagnosticSeverity.Warning,
                "未找到轨号；已按一行一轨解析，请在写入前确认结果。")]);
    }

    private static void FinalizeBlock(
        TrackBlock? block,
        ICollection<ParsedTrack> tracks,
        ICollection<CoreDiagnostic> diagnostics)
    {
        if (block is null)
        {
            return;
        }

        if (block.Content.Count == 0)
        {
            diagnostics.Add(new CoreDiagnostic(
                "PARSE_MISSING_TITLE",
                DiagnosticSeverity.Error,
                $"轨道 {block.TrackNumber:00} 缺少曲名。",
                block.SourceLine));
            return;
        }

        tracks.Add(new ParsedTrack(
            block.TrackNumber,
            block.Content[0],
            block.Content.Skip(1),
            block.SourceLine));
    }

    private static TracklistParseResult AddSequenceDiagnostics(TracklistParseResult result)
    {
        var diagnostics = result.Diagnostics.ToList();
        var duplicates = result.Tracks
            .GroupBy(track => track.TrackNumber)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicate in duplicates)
        {
            diagnostics.Add(new CoreDiagnostic(
                "PARSE_DUPLICATE_TRACK_NUMBER",
                DiagnosticSeverity.Error,
                $"轨号 {duplicate:00} 重复。"));
        }

        for (var index = 0; index < result.Tracks.Count; index++)
        {
            var expected = index + 1;
            if (result.Tracks[index].TrackNumber != expected)
            {
                diagnostics.Add(new CoreDiagnostic(
                    "PARSE_NON_SEQUENTIAL_TRACKS",
                    DiagnosticSeverity.Warning,
                    $"第 {index + 1} 条记录的轨号是 {result.Tracks[index].TrackNumber:00}，预期为 {expected:00}。",
                    result.Tracks[index].SourceLine));
            }
        }

        return new TracklistParseResult(result.Mode, result.Tracks, diagnostics);
    }

    private static bool TryParseStandaloneTrackNumber(string value, out int trackNumber)
    {
        var match = StandaloneTrackNumberRegex().Match(value);
        return TryReadTrackNumber(match, out trackNumber);
    }

    private static bool TryParseInlineTrack(string value, out int trackNumber, out string title)
    {
        var match = InlineTrackRegex().Match(value);
        if (!TryReadTrackNumber(match, out trackNumber))
        {
            title = string.Empty;
            return false;
        }

        title = match.Groups["title"].Value.Trim();
        return title.Length > 0;
    }

    private static bool TryReadTrackNumber(Match match, out int trackNumber)
    {
        trackNumber = 0;
        return match.Success
            && int.TryParse(
                match.Groups["number"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out trackNumber)
            && trackNumber > 0;
    }

    [GeneratedRegex(
        @"^(?:(?:tr(?:ack)?)\s*[.\-_:：]?\s*)?0*(?<number>[0-9]{1,3})\s*[.):：、．]?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StandaloneTrackNumberRegex();

    [GeneratedRegex(
        @"^(?:(?:tr(?:ack)?)\s*[.\-_:：]?\s*)?0*(?<number>[0-9]{1,3})\s*(?:[.):：、．\-]\s*|\s+)(?<title>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InlineTrackRegex();

    private sealed record SourceLine(int Number, string Text);

    private sealed class TrackBlock(int trackNumber, int sourceLine)
    {
        public int TrackNumber { get; } = trackNumber;

        public int SourceLine { get; } = sourceLine;

        public List<string> Content { get; } = [];
    }
}
