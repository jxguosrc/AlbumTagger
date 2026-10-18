using System.Security.Cryptography;

namespace AlbumTagger.Core.Metadata;

public sealed class CoverImage
{
    private readonly byte[] data;

    private CoverImage(byte[] data, string mimeType)
    {
        this.data = data;
        MimeType = mimeType;
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(data));
    }

    public string MimeType { get; }

    public string Sha256 { get; }

    public int Length => data.Length;

    public static CoverImage FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("封面图片不能为空。", nameof(data));
        }

        var mimeType = DetectMimeType(data)
            ?? throw new ArgumentException("封面必须是 PNG、JPEG、GIF 或 WebP 图片。", nameof(data));

        return new CoverImage(data.ToArray(), mimeType);
    }

    public static CoverImage FromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return FromBytes(File.ReadAllBytes(filePath));
    }

    public byte[] ToArray() => (byte[])data.Clone();

    private static string? DetectMimeType(ReadOnlySpan<byte> value)
    {
        if (value.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (value.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
        {
            return "image/jpeg";
        }

        if (value.StartsWith("GIF87a"u8) || value.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }

        if (value.Length >= 12
            && value[..4].SequenceEqual("RIFF"u8)
            && value.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
