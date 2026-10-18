namespace AlbumTagger.Core.Metadata;

public sealed record EmbeddedPictureSnapshot(
    string PictureType,
    string MimeType,
    string Description,
    string Sha256,
    int ByteLength);

public sealed class AudioFileSnapshot
{
    public AudioFileSnapshot(
        string filePath,
        string title,
        string album,
        string artist,
        string albumArtist,
        int? trackNumber,
        int? trackTotal,
        double durationMilliseconds,
        string audioFramesSha256,
        IEnumerable<EmbeddedPictureSnapshot> embeddedPictures,
        IReadOnlyDictionary<string, string> additionalFields)
    {
        FilePath = filePath;
        Title = title;
        Album = album;
        Artist = artist;
        AlbumArtist = albumArtist;
        TrackNumber = trackNumber;
        TrackTotal = trackTotal;
        DurationMilliseconds = durationMilliseconds;
        AudioFramesSha256 = audioFramesSha256;
        EmbeddedPictures = Array.AsReadOnly(embeddedPictures.ToArray());
        AdditionalFields = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(additionalFields, StringComparer.OrdinalIgnoreCase));
    }

    public string FilePath { get; }

    public string Title { get; }

    public string Album { get; }

    public string Artist { get; }

    public string AlbumArtist { get; }

    public int? TrackNumber { get; }

    public int? TrackTotal { get; }

    public double DurationMilliseconds { get; }

    public string AudioFramesSha256 { get; }

    public IReadOnlyList<EmbeddedPictureSnapshot> EmbeddedPictures { get; }

    public IReadOnlyDictionary<string, string> AdditionalFields { get; }
}
