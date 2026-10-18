using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AlbumTagger.Core.Metadata;

public static class FlacAudioHasher
{
    private static ReadOnlySpan<byte> FlacMarker => "fLaC"u8;

    public static string ComputeAudioFramesSha256(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);

        Span<byte> marker = stackalloc byte[4];
        ReadExactly(stream, marker, "FLAC 文件头不完整。");
        if (!marker.SequenceEqual(FlacMarker))
        {
            throw new InvalidDataException("文件不是原生 FLAC（缺少 fLaC 标记）。");
        }

        var isLastMetadataBlock = false;
        Span<byte> header = stackalloc byte[4];
        while (!isLastMetadataBlock)
        {
            ReadExactly(stream, header, "FLAC 元数据块头不完整。");
            isLastMetadataBlock = (header[0] & 0x80) != 0;
            var length = BinaryPrimitives.ReadUInt32BigEndian(header) & 0x00FF_FFFF;
            SkipExactly(stream, length);
        }

        using var sha256 = SHA256.Create();
        return Convert.ToHexStringLower(sha256.ComputeHash(stream));
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer, string errorMessage)
    {
        try
        {
            stream.ReadExactly(buffer);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException(errorMessage, exception);
        }
    }

    private static void SkipExactly(Stream stream, uint byteCount)
    {
        if (byteCount > stream.Length - stream.Position)
        {
            throw new InvalidDataException("FLAC 元数据块长度超出了文件边界。");
        }

        stream.Seek(byteCount, SeekOrigin.Current);
    }
}
