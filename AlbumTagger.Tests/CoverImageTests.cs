using AlbumTagger.Core.Metadata;

namespace AlbumTagger.Tests;

public sealed class CoverImageTests
{
    [Fact]
    public void FromBytes_DetectsPngAndClonesInput()
    {
        var bytes = TestImages.Png.ToArray();

        var cover = CoverImage.FromBytes(bytes);
        bytes[0] = 0;

        Assert.Equal("image/png", cover.MimeType);
        Assert.Equal(0x89, cover.ToArray()[0]);
        Assert.Equal(64, cover.Sha256.Length);
    }

    [Fact]
    public void FromBytes_RejectsUnknownData()
    {
        var exception = Assert.Throws<ArgumentException>(() => CoverImage.FromBytes([1, 2, 3]));

        Assert.Contains("PNG", exception.Message, StringComparison.Ordinal);
    }
}

internal static class TestImages
{
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
}
