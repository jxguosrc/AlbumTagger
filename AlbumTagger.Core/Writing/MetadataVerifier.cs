using AlbumTagger.Core.Diagnostics;
using AlbumTagger.Core.Metadata;
using AlbumTagger.Core.Planning;

namespace AlbumTagger.Core.Writing;

internal static class MetadataVerifier
{
    public static bool MatchesOriginal(AudioFileSnapshot expected, AudioFileSnapshot current)
    {
        return string.Equals(expected.Title, current.Title, StringComparison.Ordinal)
            && string.Equals(expected.Album, current.Album, StringComparison.Ordinal)
            && string.Equals(expected.Artist, current.Artist, StringComparison.Ordinal)
            && string.Equals(expected.AlbumArtist, current.AlbumArtist, StringComparison.Ordinal)
            && expected.TrackNumber == current.TrackNumber
            && expected.TrackTotal == current.TrackTotal
            && string.Equals(expected.AudioFramesSha256, current.AudioFramesSha256, StringComparison.Ordinal)
            && DictionaryEquals(expected.AdditionalFields, current.AdditionalFields)
            && PictureMultiset(expected.EmbeddedPictures).SequenceEqual(PictureMultiset(current.EmbeddedPictures));
    }

    public static IReadOnlyList<CoreDiagnostic> VerifyWrittenCopy(
        TrackWritePlan plan,
        AudioFileSnapshot written)
    {
        var diagnostics = new List<CoreDiagnostic>();
        var desired = plan.Desired;
        Check(diagnostics, written.FilePath, "TITLE", written.Title, desired.Title);
        Check(diagnostics, written.FilePath, "ALBUM", written.Album, desired.Album);
        Check<int?>(diagnostics, written.FilePath, "TRACKNUMBER", written.TrackNumber, desired.TrackNumber);
        Check<int?>(diagnostics, written.FilePath, "TRACKTOTAL", written.TrackTotal, desired.TrackTotal);

        if (desired.Artist.Mode != TextFieldEditMode.Keep)
        {
            Check(
                diagnostics,
                written.FilePath,
                "ARTIST",
                written.Artist,
                desired.Artist.ApplyTo(plan.Original.Artist));
        }

        if (desired.AlbumArtist.Mode != TextFieldEditMode.Keep)
        {
            Check(
                diagnostics,
                written.FilePath,
                "ALBUMARTIST",
                written.AlbumArtist,
                desired.AlbumArtist.ApplyTo(plan.Original.AlbumArtist));
        }

        if (!string.Equals(
                plan.Original.AudioFramesSha256,
                written.AudioFramesSha256,
                StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "VERIFY_AUDIO_CHANGED",
                $"{written.FilePath} 的 FLAC 音频帧在标签写入后发生了变化。"));
        }

        if (!DictionaryEquals(plan.Original.AdditionalFields, written.AdditionalFields))
        {
            diagnostics.Add(Error(
                "VERIFY_UNKNOWN_TAGS_CHANGED",
                $"{written.FilePath} 的未管理标签在写入后发生了变化。"));
        }

        VerifyPictures(plan, written, diagnostics);
        return diagnostics;
    }

    private static void VerifyPictures(
        TrackWritePlan plan,
        AudioFileSnapshot written,
        ICollection<CoreDiagnostic> diagnostics)
    {
        var originalNonFront = PictureMultiset(plan.Original.EmbeddedPictures.Where(picture => !IsFront(picture)));
        var writtenNonFront = PictureMultiset(written.EmbeddedPictures.Where(picture => !IsFront(picture)));
        if (!originalNonFront.SequenceEqual(writtenNonFront))
        {
            diagnostics.Add(Error(
                "VERIFY_OTHER_PICTURES_CHANGED",
                $"{written.FilePath} 的非正面封面图片在写入后发生了变化。"));
        }

        if (plan.Desired.FrontCover is null)
        {
            var originalFront = PictureMultiset(plan.Original.EmbeddedPictures.Where(IsFront));
            var writtenFront = PictureMultiset(written.EmbeddedPictures.Where(IsFront));
            if (!originalFront.SequenceEqual(writtenFront))
            {
                diagnostics.Add(Error(
                    "VERIFY_FRONT_COVER_CHANGED",
                    $"{written.FilePath} 的正面封面在未要求修改时发生了变化。"));
            }

            return;
        }

        var writtenFrontCovers = written.EmbeddedPictures.Where(IsFront).ToArray();
        if (writtenFrontCovers.Length != 1
            || !string.Equals(
                writtenFrontCovers[0].Sha256,
                plan.Desired.FrontCover.Sha256,
                StringComparison.Ordinal))
        {
            diagnostics.Add(Error(
                "VERIFY_FRONT_COVER_FAILED",
                $"{written.FilePath} 没有包含计划中的唯一正面封面。"));
        }
    }

    private static bool IsFront(EmbeddedPictureSnapshot picture) =>
        string.Equals(picture.PictureType, "Front", StringComparison.Ordinal);

    private static IEnumerable<string> PictureMultiset(IEnumerable<EmbeddedPictureSnapshot> pictures) =>
        pictures.Select(picture => string.Join(
                '\u001F',
                picture.PictureType,
                picture.MimeType,
                picture.Description,
                picture.Sha256,
                picture.ByteLength))
            .Order(StringComparer.Ordinal);

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(pair => right.TryGetValue(pair.Key, out var value)
                                && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }

    private static void Check<T>(
        ICollection<CoreDiagnostic> diagnostics,
        string filePath,
        string field,
        T actual,
        T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            diagnostics.Add(Error(
                "VERIFY_FIELD_MISMATCH",
                $"{filePath} 的 {field} 写入校验失败；实际值为“{actual}”，计划值为“{expected}”。"));
        }
    }

    private static CoreDiagnostic Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);
}
