namespace AlbumTagger.Core.Metadata;

public interface IFlacMetadataStore
{
    AudioFileSnapshot Read(string filePath);

    void Apply(string filePath, DesiredTrackMetadata metadata);
}

public sealed record DesiredTrackMetadata(
    string Title,
    string Album,
    int TrackNumber,
    int TrackTotal,
    TextFieldEdit Artist,
    TextFieldEdit AlbumArtist,
    CoverImage? FrontCover);
