using CommunityToolkit.Mvvm.ComponentModel;

namespace AlbumTagger.App.ViewModels;

public sealed class TrackRowViewModel : ObservableObject
{
    private string title;
    private string artist = string.Empty;
    private string existingTitle;
    private string changeSummary = "尚未检查写入计划";

    public TrackRowViewModel(
        string filePath,
        string fileName,
        int trackNumber,
        string parsedTitle,
        string rawMetadata,
        string existingTitle)
    {
        FilePath = filePath;
        FileName = fileName;
        TrackNumber = trackNumber;
        title = parsedTitle;
        RawMetadata = rawMetadata;
        this.existingTitle = existingTitle;
    }

    public string FilePath { get; }

    public string FileName { get; }

    public int TrackNumber { get; }

    public string RawMetadata { get; }

    public string ExistingTitle
    {
        get => existingTitle;
        set => SetProperty(ref existingTitle, value);
    }

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public string Artist
    {
        get => artist;
        set => SetProperty(ref artist, value);
    }

    public string ChangeSummary
    {
        get => changeSummary;
        set => SetProperty(ref changeSummary, value);
    }
}
