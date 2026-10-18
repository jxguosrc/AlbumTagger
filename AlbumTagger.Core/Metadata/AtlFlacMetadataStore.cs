using System.Security.Cryptography;
using ATL;

namespace AlbumTagger.Core.Metadata;

public sealed class AtlFlacMetadataStore : IFlacMetadataStore
{
    public AudioFileSnapshot Read(string filePath)
    {
        ValidateFlacPath(filePath);
        var track = new Track(filePath);

        var pictures = track.EmbeddedPictures.Select(picture =>
        {
            var pictureData = picture.PictureData ?? [];
            return new EmbeddedPictureSnapshot(
                picture.PicType.ToString(),
                picture.MimeType ?? string.Empty,
                picture.Description ?? string.Empty,
                Convert.ToHexStringLower(SHA256.HashData(pictureData)),
                pictureData.Length);
        });

        return new AudioFileSnapshot(
            Path.GetFullPath(filePath),
            track.Title ?? string.Empty,
            track.Album ?? string.Empty,
            track.Artist ?? string.Empty,
            track.AlbumArtist ?? string.Empty,
            track.TrackNumber,
            track.TrackTotal,
            track.DurationMs,
            FlacAudioHasher.ComputeAudioFramesSha256(filePath),
            pictures,
            track.AdditionalFields.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase));
    }

    public void Apply(string filePath, DesiredTrackMetadata metadata)
    {
        ValidateFlacPath(filePath);
        ArgumentNullException.ThrowIfNull(metadata);

        var track = new Track(filePath)
        {
            Title = metadata.Title,
            Album = metadata.Album,
            TrackNumber = metadata.TrackNumber,
            TrackTotal = metadata.TrackTotal,
        };

        if (metadata.Artist.Mode != TextFieldEditMode.Keep)
        {
            track.Artist = metadata.Artist.ApplyTo(track.Artist ?? string.Empty);
        }

        if (metadata.AlbumArtist.Mode != TextFieldEditMode.Keep)
        {
            track.AlbumArtist = metadata.AlbumArtist.ApplyTo(track.AlbumArtist ?? string.Empty);
        }

        if (metadata.FrontCover is not null)
        {
            var oldFrontCovers = track.EmbeddedPictures
                .Where(picture => picture.PicType == PictureInfo.PIC_TYPE.Front)
                .ToArray();
            foreach (var picture in oldFrontCovers)
            {
                track.EmbeddedPictures.Remove(picture);
            }

            var newCover = PictureInfo.fromBinaryData(
                metadata.FrontCover.ToArray(),
                PictureInfo.PIC_TYPE.Front);
            track.EmbeddedPictures.Add(newCover);
        }

        if (!track.Save())
        {
            throw new IOException($"ATL.NET 无法写入 FLAC 标签：{filePath}");
        }
    }

    private static void ValidateFlacPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!string.Equals(Path.GetExtension(filePath), ".flac", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("文件扩展名必须是 .flac。", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("找不到 FLAC 文件。", filePath);
        }
    }
}
