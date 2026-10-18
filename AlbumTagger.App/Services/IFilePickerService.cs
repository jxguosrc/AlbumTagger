namespace AlbumTagger.App.Services;

public interface IFilePickerService
{
    Task<string?> PickFolderAsync();

    Task<string?> PickCoverImageAsync();
}
