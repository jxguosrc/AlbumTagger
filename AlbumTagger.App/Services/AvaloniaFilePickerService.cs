using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AlbumTagger.App.Services;

public sealed class AvaloniaFilePickerService(TopLevel owner) : IFilePickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择包含一张专辑 FLAC 的文件夹",
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickCoverImageAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择要嵌入的封面图片",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("封面图片")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp"],
                    MimeTypes = ["image/png", "image/jpeg", "image/gif", "image/webp"],
                },
            ],
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
