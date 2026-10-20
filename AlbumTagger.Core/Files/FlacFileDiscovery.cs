namespace AlbumTagger.Core.Files;

public sealed record AudioFileReference(
    string FilePath,
    string FileName,
    long LengthBytes,
    int? ExistingTrackNumber = null);

public sealed class FlacFileDiscovery
{
    public IReadOnlyList<AudioFileReference> Discover(string folderPath, bool recursive = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"找不到专辑文件夹：{fullPath}");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(fullPath, "*", searchOption)
            .Where(path => string.Equals(Path.GetExtension(path), ".flac", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, NaturalStringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var file = new FileInfo(path);
                return new AudioFileReference(file.FullName, file.Name, file.Length);
            })
            .ToArray();

        return Array.AsReadOnly(files);
    }
}
