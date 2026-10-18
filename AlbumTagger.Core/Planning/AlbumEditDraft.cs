using AlbumTagger.Core.Metadata;

namespace AlbumTagger.Core.Planning;

public sealed record TrackEditDraft(
    string FilePath,
    int TrackNumber,
    string Title,
    TextFieldEdit Artist = default);

public sealed record AlbumEditDraft(
    string AlbumTitle,
    IReadOnlyList<TrackEditDraft> Tracks,
    TextFieldEdit AlbumArtist = default,
    CoverImage? FrontCover = null);
