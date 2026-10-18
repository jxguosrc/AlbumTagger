using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Metadata;

namespace AlbumTagger.Core.Planning;

public sealed class AlbumWritePlanBuilder(IFlacMetadataStore metadataStore)
{
    public AlbumWritePlan Build(AlbumEditDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(draft.Tracks);

        var diagnostics = Validate(draft);
        if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new AlbumWritePlan([], diagnostics);
        }

        var trackPlans = new List<TrackWritePlan>(draft.Tracks.Count);
        var trackTotal = draft.Tracks.Count;
        foreach (var trackDraft in draft.Tracks)
        {
            try
            {
                var snapshot = metadataStore.Read(trackDraft.FilePath);
                var desired = new DesiredTrackMetadata(
                    trackDraft.Title.Trim(),
                    draft.AlbumTitle.Trim(),
                    trackDraft.TrackNumber,
                    trackTotal,
                    trackDraft.Artist,
                    draft.AlbumArtist,
                    draft.FrontCover);

                trackPlans.Add(new TrackWritePlan(snapshot, desired, DescribeChanges(snapshot, desired)));
            }
            catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or InvalidDataException
                                               or ArgumentException)
            {
                diagnostics.Add(new CoreDiagnostic(
                    "PLAN_READ_FAILED",
                    DiagnosticSeverity.Error,
                    $"无法读取 {trackDraft.FilePath}：{exception.Message}"));
            }
        }

        return new AlbumWritePlan(trackPlans, diagnostics);
    }

    private static List<CoreDiagnostic> Validate(AlbumEditDraft draft)
    {
        var diagnostics = new List<CoreDiagnostic>();
        if (string.IsNullOrWhiteSpace(draft.AlbumTitle))
        {
            diagnostics.Add(new CoreDiagnostic(
                "PLAN_ALBUM_REQUIRED",
                DiagnosticSeverity.Error,
                "专辑名不能为空。"));
        }

        if (draft.Tracks.Count == 0)
        {
            diagnostics.Add(new CoreDiagnostic(
                "PLAN_NO_TRACKS",
                DiagnosticSeverity.Error,
                "写入计划中没有曲目。"));
            return diagnostics;
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var numbers = new HashSet<int>();
        for (var index = 0; index < draft.Tracks.Count; index++)
        {
            var track = draft.Tracks[index];
            if (string.IsNullOrWhiteSpace(track.FilePath))
            {
                diagnostics.Add(Error("PLAN_FILE_REQUIRED", $"第 {index + 1} 轨没有文件路径。"));
            }
            else
            {
                var fullPath = Path.GetFullPath(track.FilePath);
                if (!paths.Add(fullPath))
                {
                    diagnostics.Add(Error("PLAN_DUPLICATE_FILE", $"同一个文件被匹配了多次：{fullPath}"));
                }

                if (!string.Equals(Path.GetExtension(fullPath), ".flac", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(Error("PLAN_NOT_FLAC", $"文件不是 FLAC：{fullPath}"));
                }
                else if (!File.Exists(fullPath))
                {
                    diagnostics.Add(Error("PLAN_FILE_NOT_FOUND", $"找不到文件：{fullPath}"));
                }
            }

            if (track.TrackNumber <= 0)
            {
                diagnostics.Add(Error("PLAN_INVALID_TRACK_NUMBER", $"第 {index + 1} 条记录的轨号必须大于零。"));
            }
            else if (!numbers.Add(track.TrackNumber))
            {
                diagnostics.Add(Error("PLAN_DUPLICATE_TRACK_NUMBER", $"轨号 {track.TrackNumber:00} 重复。"));
            }

            if (track.TrackNumber != index + 1)
            {
                diagnostics.Add(Error(
                    "PLAN_NON_SEQUENTIAL_TRACKS",
                    $"第 {index + 1} 条记录的轨号是 {track.TrackNumber:00}；当前版本只写入从 01 开始的连续单碟专辑。"));
            }

            if (string.IsNullOrWhiteSpace(track.Title))
            {
                diagnostics.Add(Error("PLAN_TITLE_REQUIRED", $"轨道 {track.TrackNumber:00} 的曲名不能为空。"));
            }
        }

        return diagnostics;
    }

    private static IEnumerable<TagFieldChange> DescribeChanges(
        AudioFileSnapshot snapshot,
        DesiredTrackMetadata desired)
    {
        var changes = new List<TagFieldChange>();
        AddTextChange(changes, "TITLE", snapshot.Title, desired.Title);
        AddTextChange(changes, "ALBUM", snapshot.Album, desired.Album);
        AddTextChange(
            changes,
            "TRACKNUMBER",
            snapshot.TrackNumber?.ToString() ?? string.Empty,
            desired.TrackNumber.ToString());
        AddTextChange(
            changes,
            "TRACKTOTAL",
            snapshot.TrackTotal?.ToString() ?? string.Empty,
            desired.TrackTotal.ToString());

        if (desired.Artist.Mode != TextFieldEditMode.Keep)
        {
            AddTextChange(changes, "ARTIST", snapshot.Artist, desired.Artist.ApplyTo(snapshot.Artist));
        }

        if (desired.AlbumArtist.Mode != TextFieldEditMode.Keep)
        {
            AddTextChange(
                changes,
                "ALBUMARTIST",
                snapshot.AlbumArtist,
                desired.AlbumArtist.ApplyTo(snapshot.AlbumArtist));
        }

        if (desired.FrontCover is not null)
        {
            var oldFrontCovers = snapshot.EmbeddedPictures
                .Where(picture => string.Equals(picture.PictureType, "Front", StringComparison.Ordinal))
                .Select(picture => picture.Sha256)
                .ToArray();
            if (oldFrontCovers.Length != 1
                || !string.Equals(oldFrontCovers[0], desired.FrontCover.Sha256, StringComparison.Ordinal))
            {
                changes.Add(new TagFieldChange(
                    "COVER_FRONT",
                    oldFrontCovers.Length == 0 ? null : string.Join(", ", oldFrontCovers),
                    desired.FrontCover.Sha256));
            }
        }

        return changes;
    }

    private static void AddTextChange(
        ICollection<TagFieldChange> changes,
        string field,
        string before,
        string after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new TagFieldChange(field, before, after));
        }
    }

    private static CoreDiagnostic Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);
}
